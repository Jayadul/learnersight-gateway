using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.Infrastructure.Security;

public class AesEncryptionService(IConfiguration config) : IEncryptionService
{
    private byte[] Key
    {
        get
        {
            var raw = config["Encryption:Key"]
                ?? throw new InvalidOperationException("Encryption:Key is not configured.");
            // Pad or truncate to 32 bytes for AES-256
            var keyBytes = new byte[32];
            var src = Encoding.UTF8.GetBytes(raw);
            Buffer.BlockCopy(src, 0, keyBytes, 0, Math.Min(src.Length, 32));
            return keyBytes;
        }
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();

        using var enc = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV (16 bytes) to ciphertext, then Base64-encode
        var result = new byte[16 + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
        Buffer.BlockCopy(cipherBytes, 0, result, 16, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var fullBytes = Convert.FromBase64String(cipherText);
        var iv = new byte[16];
        var cipher = new byte[fullBytes.Length - 16];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
        Buffer.BlockCopy(fullBytes, 16, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;

        using var dec = aes.CreateDecryptor();
        var plainBytes = dec.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
