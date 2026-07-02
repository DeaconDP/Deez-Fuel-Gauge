using System.Security.Cryptography;
using System.Text;

namespace DeezFuelGauge.Services.Credentials;

internal static class CredentialProtector
{
    public static byte[] Protect(byte[] data) => AesProtect(data, encrypt: true, AppBranding.SettingsSlug);

    public static byte[] Unprotect(byte[] data)
    {
        try
        {
            return UnprotectWithSlug(data, AppBranding.SettingsSlug);
        }
        catch (CryptographicException)
        {
            return UnprotectWithSlug(data, AppBranding.LegacySettingsSlug);
        }
    }

    internal static byte[] ProtectWithSlug(byte[] data, string settingsSlug) =>
        AesProtect(data, encrypt: true, settingsSlug);

    internal static byte[] UnprotectWithSlug(byte[] data, string settingsSlug) =>
        AesProtect(data, encrypt: false, settingsSlug);

    private static byte[] AesProtect(byte[] data, bool encrypt, string settingsSlug)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + ":" + settingsSlug));

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
