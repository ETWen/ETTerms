using System.IO.Ports;
using ETTerms.Connections;
using ETTerms.Infrastructure;

namespace ETTerms.Sessions;

/// <summary>
/// Serial 連線通道：包 <see cref="SerialPort"/>。開啟前向 SessionManager 占用 COM port（互斥）。
/// </summary>
public sealed class SerialChannel : ISessionChannel
{
    private readonly SerialPort _port;
    private readonly string _portName;
    private bool _opened;

    public event Action<byte[]>? DataReceived;

    public string LogName => _portName;

    public SerialChannel(SerialSettings s)
    {
        _portName = s.PortName;
        _port = new SerialPort(s.PortName, s.BaudRate, s.Parity, s.DataBits, s.StopBits)
        {
            Handshake = s.Handshake,
            NewLine = s.NewLine
        };
        _port.DataReceived += OnData;
    }

    public void Open()
    {
        if (!SessionManager.TryReservePort(_portName))
            throw new InvalidOperationException($"COM port {_portName} is already in use by another session.");
        try
        {
            _port.Open();
            _opened = true;
            AppLogger.Info($"Serial opened: {_portName} @ {_port.BaudRate}");
        }
        catch
        {
            SessionManager.ReleasePort(_portName);
            throw;
        }
    }

    private void OnData(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            int n = _port.BytesToRead;
            if (n <= 0) return;
            var buf = new byte[n];
            int read = _port.Read(buf, 0, n);
            if (read > 0) DataReceived?.Invoke(read == n ? buf : buf[..read]);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"Serial read error on {_portName}: {ex.Message}");
        }
    }

    public void Write(byte[] data)
    {
        if (_port.IsOpen) _port.Write(data, 0, data.Length);
    }

    public void Resize(int cols, int rows) { /* serial 無 PTY size */ }

    public void Close()
    {
        if (!_opened) return;
        _opened = false;
        try { if (_port.IsOpen) _port.Close(); } catch { /* 忽略關閉錯誤 */ }
        SessionManager.ReleasePort(_portName);
        AppLogger.Info($"Serial closed: {_portName}");
    }

    public void Dispose()
    {
        _port.DataReceived -= OnData;
        Close();
        _port.Dispose();
    }
}
