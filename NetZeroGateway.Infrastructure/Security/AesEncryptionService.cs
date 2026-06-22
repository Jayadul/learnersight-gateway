using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.Infrastructure.Security;

public class AesEncryptionService(IConfiguration config) : IEncryptionService
{
    private const string KeyPath = "Encryption:Key";

    private byte[] DerivedKey
    {
        get
        {
            var raw = config[KeyPath]
                ?? throw new InvalidOperationException($"{KeyPath} is not configured.");
            var keyBytes = new byte[32];
            var src = Encoding.UTF8.GetBytes(raw);
            Buffer.BlockCopy(src, 0, keyBytes, 0, Math.Min(src.Length, 32));
            return keyBytes;
        }
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = DerivedKey;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();
        var cipher = aes.CreateEncryptor()
            .TransformFinalBlock(Encoding.UTF8.GetBytes(plainText), 0,
                Encoding.UTF8.GetBytes(plainText).Length);
        var result = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
        Buffer.BlockCopy(cipher, 0, result, 16, cipher.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var full = Convert.FromBase64String(cipherText);
        var iv = new byte[16];
        var cipher = new byte[full.Length - 16];
        Buffer.BlockCopy(full, 0, iv, 0, 16);
        Buffer.BlockCopy(full, 16, cipher, 0, cipher.Length);
        using var aes = Aes.Create();
        aes.Key = DerivedKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        return Encoding.UTF8.GetString(
            aes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length));
    }
}