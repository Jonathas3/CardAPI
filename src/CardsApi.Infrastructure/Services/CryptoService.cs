using System.Security.Cryptography;
using System.Text;
using CardsApi.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace CardsApi.Infrastructure.Services;

public class CryptoService : ICryptoService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public CryptoService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Crypto:AesKeyBase64"]
            ?? throw new InvalidOperationException("Crypto:AesKeyBase64 is not configured.");
        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Crypto:AesKeyBase64 must decode to exactly 32 bytes (AES-256).");
        }
    }

    public string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var combined = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length + cipherBytes.Length, tag.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherTextBase64)
    {
        var combined = Convert.FromBase64String(cipherTextBase64);
        var cipherLength = combined.Length - NonceSizeBytes - TagSizeBytes;

        var nonce = new byte[NonceSizeBytes];
        var cipherBytes = new byte[cipherLength];
        var tag = new byte[TagSizeBytes];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(combined, NonceSizeBytes, cipherBytes, 0, cipherLength);
        Buffer.BlockCopy(combined, NonceSizeBytes + cipherLength, tag, 0, TagSizeBytes);

        var plainBytes = new byte[cipherLength];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
