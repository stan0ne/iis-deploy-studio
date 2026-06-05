using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace IISDeploy.Infrastructure.Security;

public static class CredentialManager
{
    /// <summary>
    /// Encrypts a string using DPAPI (machine-level, for the current user scope).
    /// Used for temporary in-memory protection of passwords during import operations.
    /// </summary>
    public static string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts a DPAPI-protected string.
    /// </summary>
    public static string Unprotect(string protectedText)
    {
        var protectedBytes = Convert.FromBase64String(protectedText);
        var bytes = ProtectedData.Unprotect(protectedBytes, null,
            DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Creates a SecureString from a plain text password.
    /// </summary>
    public static SecureString ToSecureString(string password)
    {
        var secure = new SecureString();
        foreach (char c in password)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }

    /// <summary>
    /// Extracts plain text from a SecureString (use with caution).
    /// </summary>
    public static string FromSecureString(SecureString secure)
    {
        var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secure);
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr) ?? string.Empty;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
        }
    }

    /// <summary>
    /// Securely erases a byte array from memory.
    /// </summary>
    public static void EraseBytes(byte[] data)
    {
        if (data is null) return;
        Array.Clear(data);
        CryptographicOperations.ZeroMemory(data.AsSpan());
    }
}
