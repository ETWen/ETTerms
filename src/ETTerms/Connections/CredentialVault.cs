using System.Runtime.InteropServices;
using System.Text;
using ETTerms.Infrastructure;

namespace ETTerms.Connections;

/// <summary>
/// Windows Credential Manager 封裝（advapi32 P/Invoke）。
/// 連線密碼 / passphrase 一律存這裡，SQLite 只存 CredentialKey。明碼絕不落地。
/// </summary>
public static class CredentialVault
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);

    /// <summary>寫入 / 覆寫一筆密碼。</summary>
    public static void Set(string key, string secret)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(secret);
        IntPtr blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = key,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = "ETTerms"
            };
            if (!CredWrite(ref cred, 0))
                AppLogger.LogError($"CredWrite failed for {key}: {Marshal.GetLastWin32Error()}");
        }
        finally { Marshal.FreeCoTaskMem(blob); }
    }

    /// <summary>讀取密碼；不存在回傳 null。</summary>
    public static string? Get(string key)
    {
        if (!CredRead(key, CRED_TYPE_GENERIC, 0, out IntPtr ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            if (cred.CredentialBlobSize == 0) return "";
            byte[] bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally { CredFree(ptr); }
    }

    /// <summary>刪除密碼（不存在則忽略）。</summary>
    public static void Delete(string key) => CredDelete(key, CRED_TYPE_GENERIC, 0);
}
