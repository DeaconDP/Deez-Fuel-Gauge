using System.Security.Cryptography;
using System.Text;

namespace CursorUsageWidget.Services.Credentials;

internal static class CredentialProtector
{
    public static byte[] Protect(byte[] data) => AesProtect(data, encrypt: true);

    public static byte[] Unprotect(byte[] data) => AesProtect(data, encrypt: false);

    private static byte[] AesProtect(byte[] data, bool encrypt)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + ":cursor-usage-widget"));

        using var aes = Aes.Create();
        aes.Key = key;

        if (encrypt)
        {
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return result;
        }

        var iv = new byte[aes.BlockSize / 8];
        var cipherBytes = new byte[data.Length - iv.Length];
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(data, iv.Length, cipherBytes, 0, cipherBytes.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }
}
