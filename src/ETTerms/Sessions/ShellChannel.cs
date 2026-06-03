using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ETTerms.Connections;
using Microsoft.Win32.SafeHandles;

namespace ETTerms.Sessions;

/// <summary>
/// Local shell channel using Windows ConPTY for proper PTY support.
/// This allows PowerShell/Bash/Cmd to behave like a real terminal with
/// arrow keys, history, backspace, PSReadLine, etc.
/// </summary>
public sealed class ShellChannel : ISessionChannel
{
    private readonly ShellSettings _settings;
    private SafeFileHandle? _ptyOutput;
    private SafeFileHandle? _ptyInput;
    private FileStream? _writeStream;
    private IntPtr _ptyHandle;
    private Process? _proc;
    private Thread? _reader;
    private bool _closed;

    public event Action<byte[]>? DataReceived;

    public ShellChannel(ShellSettings settings) => _settings = settings;

    public void Open()
    {
        // Create pipes
        CreatePipe(out var inputReadSide, out var inputWriteSide);
        CreatePipe(out var outputReadSide, out var outputWriteSide);

        // Create pseudo console (ConPTY)
        var size = new COORD { X = 120, Y = 30 };
        int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out _ptyHandle);
        if (hr != 0) throw new Exception($"CreatePseudoConsole failed: 0x{hr:X8}");

        // Close handles that are now owned by the pseudo console
        inputReadSide.Dispose();
        outputWriteSide.Dispose();

        _ptyInput = inputWriteSide;
        _ptyOutput = outputReadSide;
        _writeStream = new FileStream(_ptyInput, FileAccess.Write, 256, false);

        // Start the shell process attached to the pseudo console
        var (exe, args) = _settings.ShellType.ToLower() switch
        {
            "bash" => ("bash", "--login -i"),
            "cmd" => ("cmd.exe", ""),
            _ => ("powershell.exe", "-NoLogo")
        };

        var si = new STARTUPINFOEX();
        si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        // Initialize thread attribute list
        IntPtr attrSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
        si.lpAttributeList = Marshal.AllocHGlobal(attrSize.ToInt32());
        InitializeProcThreadAttributeList(si.lpAttributeList, 1, 0, ref attrSize);
        UpdateProcThreadAttribute(si.lpAttributeList, 0, (IntPtr)0x00020016 /* PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE */,
            _ptyHandle, IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

        string workDir = string.IsNullOrWhiteSpace(_settings.StartupDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : _settings.StartupDirectory;

        bool ok = CreateProcess(null, $"{exe} {args}".TrimEnd(), IntPtr.Zero, IntPtr.Zero, false,
            0x00080000 /* EXTENDED_STARTUPINFO_PRESENT */, IntPtr.Zero, workDir, ref si, out var pi);

        if (!ok) throw new Exception($"CreateProcess failed: {Marshal.GetLastWin32Error()}");

        CloseHandle(pi.hThread);
        _proc = Process.GetProcessById(pi.dwProcessId);

        // Start reader thread
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "ConPTY-Reader" };
        _reader.Start();
    }

    private void ReadLoop()
    {
        var buf = new byte[4096];
        try
        {
            using var stream = new FileStream(_ptyOutput!, FileAccess.Read, 4096, false);
            while (!_closed)
            {
                int n = stream.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                var data = new byte[n];
                Buffer.BlockCopy(buf, 0, data, 0, n);
                DataReceived?.Invoke(data);
            }
        }
        catch when (_closed) { }
    }

    public void Write(byte[] data)
    {
        if (_closed || _writeStream == null) return;
        _writeStream.Write(data, 0, data.Length);
        _writeStream.Flush();
    }

    public void Resize(int cols, int rows)
    {
        if (_ptyHandle != IntPtr.Zero)
            ResizePseudoConsole(_ptyHandle, new COORD { X = (short)cols, Y = (short)rows });
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _writeStream?.Dispose();
        if (_ptyHandle != IntPtr.Zero) { ClosePseudoConsole(_ptyHandle); _ptyHandle = IntPtr.Zero; }
        _ptyInput?.Dispose();
        _ptyOutput?.Dispose();
        if (_proc is { HasExited: false }) { try { _proc.Kill(); } catch { } }
        _proc?.Dispose();
    }

    public void Dispose() => Close();

    // ── Win32 Interop ──

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct STARTUPINFO
    {
        public int cb; public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll")]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CreateProcess(string? lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private static void CreatePipe(out SafeFileHandle read, out SafeFileHandle write)
    {
        if (!CreatePipe(out read, out write, IntPtr.Zero, 0))
            throw new Exception($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
    }
}
