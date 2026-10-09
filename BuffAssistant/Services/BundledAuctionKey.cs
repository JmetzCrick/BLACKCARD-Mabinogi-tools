using System.Security.Cryptography;
using System.Text;

namespace BuffAssistant.Services;

public static class BundledAuctionKey
{
    public static string Read()
    {
        using var stream = typeof(BundledAuctionKey).Assembly.GetManifestResourceStream("BlackCardHelper.AuctionKey")
            ?? throw new InvalidOperationException("내장 경매장 설정을 읽을 수 없습니다. 최신 배포본을 다시 설치해 주세요.");
        using var buffer = new System.IO.MemoryStream();
        stream.CopyTo(buffer);
        return Decrypt(buffer.ToArray());
    }
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
