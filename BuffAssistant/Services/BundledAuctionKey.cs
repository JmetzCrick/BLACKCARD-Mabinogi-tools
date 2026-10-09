using System.Security.Cryptography;
using System.Text;

namespace BuffAssistant.Services;

public static class BundledAuctionKey
{
    public static string Decrypt(byte[] package)
    {
        if (package.Length < 29) throw new CryptographicException("배포 API 키 파일이 손상되었습니다.");
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("BlackCardHelper.Beta01.SharedKey.20261010"));
        var plain = new byte[package.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(package.AsSpan(0, 12), package.AsSpan(28), package.AsSpan(12, 16), plain);
            return Encoding.UTF8.GetString(plain);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
}
