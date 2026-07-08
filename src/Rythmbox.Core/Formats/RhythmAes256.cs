using System.Security.Cryptography;
using System.Text;

namespace Rythmbox.Core.Formats;

/// <summary>AES-256-CBC helpers for <c>.apak</c> payloads (no compression).</summary>
public static class RhythmAes256
{
    public const int KeySizeBytes = 32;
    public const int IvSizeBytes = 16;

    /// <summary>Factory / internal key for RhythmLive-distributed <c>.apak</c> packs.</summary>
    public static byte[] FactoryKey { get; } = SHA256.HashData(Encoding.UTF8.GetBytes("RhythmLive.Factory.APAK.v1"));

    public static byte[] DeriveKeyFromPassword(string password, ReadOnlySpan<byte> salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt.ToArray(),
            iterations: 120_000,
            HashAlgorithmName.SHA256,
            outputLength: KeySizeBytes);
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> key32, out byte[] iv16)
    {
        ValidateKey(key32);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key32.ToArray();
        aes.GenerateIV();
        iv16 = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plain.ToArray(), 0, plain.Length);
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> cipher, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> iv16)
    {
        ValidateKey(key32);
        if (iv16.Length != IvSizeBytes)
        {
            throw new FormatException($"APAK IV must be {IvSizeBytes} bytes.");
        }

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key32.ToArray();
        aes.IV = iv16.ToArray();

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipher.ToArray(), 0, cipher.Length);
    }

    private static void ValidateKey(ReadOnlySpan<byte> key32)
    {
        if (key32.Length != KeySizeBytes)
        {
            throw new ArgumentException($"AES-256 key must be {KeySizeBytes} bytes.", nameof(key32));
        }
    }
}
