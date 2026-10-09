using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Linq;
using Tesseract;

namespace BuffAssistant.Services;

/// <summary>
/// Tesseract OCR wrapper. Put Korean language data at ./tessdata/kor.traineddata.
/// Add eng.traineddata too if the game UI includes English text.
/// </summary>
public sealed class OcrService : IDisposable
{
    private TesseractEngine? _engine;
    private Exception? _initializationError;

    public OcrService()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (Directory.Exists(dataPath) && File.Exists(Path.Combine(dataPath, "kor.traineddata")))
        {
            try { _engine = new TesseractEngine(dataPath, "kor", EngineMode.LstmOnly); }
            catch (Exception ex) { _initializationError = ex; }
        }
    }

    public IReadOnlyList<OcrWord> ReadWords(Bitmap bitmap) =>
        ReadWordsAtScale(bitmap, 1).Concat(ReadWordsAtScale(bitmap, 4)).Concat(ReadWordsAtScale(bitmap, 4, true)).ToList();

    private IReadOnlyList<OcrWord> ReadWordsAtScale(Bitmap bitmap, int scale, bool adaptive = false)
    {
        if (_engine is null)
        {
            if (_initializationError is not null)
                throw new InvalidOperationException("OCR 초기화에 실패했습니다: " + _initializationError.Message, _initializationError);
            throw new InvalidOperationException("OCR 언어 파일이 없습니다. README의 tessdata 설치 안내를 확인하세요.");
        }

        using var prepared = scale == 1 ? (Bitmap)bitmap.Clone() : PrepareText(bitmap, scale, isolateText: adaptive, adaptive: adaptive);
        _engine.SetVariable("tessedit_char_whitelist", "");
        using var stream = new MemoryStream();
        prepared.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        using var page = _engine.Process(pix, PageSegMode.SparseText);
        var words = new List<OcrWord>();
        using var iterator = page.GetIterator();
        iterator.Begin();

        do
        {
            var text = iterator.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var box))
                words.Add(new OcrWord(text.Trim(), box.X1 / scale, box.Y1 / scale,
                    Math.Max(1, (box.Width + scale - 1) / scale), Math.Max(1, (box.Height + scale - 1) / scale)));
        } while (iterator.Next(PageIteratorLevel.Word));

        return words;
    }

    public int? ReadRemainingSeconds(Bitmap bitmap, Rectangle region)
        => ReadRemainingSeconds(bitmap, region, out _);

    public int? ReadRemainingSeconds(Bitmap bitmap, Rectangle region, out string recognizedText)
    {
        var seconds = ReadTimerPass(bitmap, region, out recognizedText, false);
        if (seconds is not null && (seconds != 30 || IsConfirmedThirtySeconds(bitmap, region, recognizedText))) return seconds;
        return ReadTimerPass(bitmap, region, out recognizedText, true);
    }

    private int? ReadTimerPass(Bitmap bitmap, Rectangle region, out string recognizedText, bool adaptive)
    {
        recognizedText = "";
        if (_engine is null) return null;
        region.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        if (region.Width <= 0 || region.Height <= 0) return null;
        using var cropped = bitmap.Clone(region, PixelFormat.Format24bppRgb);
        using var prepared = PrepareText(cropped, 4, adaptive: adaptive, redOnly: adaptive && HasRedText(cropped));
        using var stream = new MemoryStream();
        prepared.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        _engine.SetVariable("tessedit_char_whitelist", "0123456789분초:");
        try
        {
            using var page = _engine.Process(pix, PageSegMode.SingleLine);
            recognizedText = page.GetText().Trim();
            var seconds = ParseRemainingSeconds(recognizedText);
            // Tiny fonts may lose both Korean unit labels: white "1분 49초"
            // becomes "149". Interpret this only as a white minute timer.
            var digits = Regex.Replace(recognizedText, @"\s+", "");
            if (seconds is null && Regex.IsMatch(digits, @"^\d{3,4}$") && !HasRedText(cropped))
            {
                var minuteSeconds = int.Parse(digits.Substring(digits.Length - 2));
                if (minuteSeconds < 60)
                    seconds = int.Parse(digits.Substring(0, digits.Length - 2)) * 60 + minuteSeconds;
            }
            return seconds;
        }
        finally { _engine.SetVariable("tessedit_char_whitelist", ""); }
    }

    private static bool HasRedText(Bitmap bitmap)
    {
        using var ink = TextContrast.CreateMask(bitmap);
        var red = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (ink.GetPixel(x, y).R > 128) continue;
            var c = bitmap.GetPixel(x, y);
            if (c.R >= 105 && c.R - Math.Max(c.G, c.B) > 55) red++;
        }
        return red >= 12;
    }

    public bool IsConfirmedThirtySeconds(Bitmap bitmap, Rectangle region, string recognizedText)
    {
        var text = Regex.Replace(recognizedText, @"\s+", "");
        // A bare "30" could be a truncated minute timer; units must be explicit.
        if (text != "30초" && text != "0분30초" && text != "0:30" && text != "00:30") return false;
        region.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        if (region.Width <= 0 || region.Height <= 0) return false;
        using var cropped = bitmap.Clone(region, PixelFormat.Format24bppRgb);
        return HasRedText(cropped);
    }

    public static int? ParseRemainingSeconds(string text)
    {
        text = Regex.Replace(text, @"\s+", "");
        var match = Regex.Match(text, @"^(?:(\d{1,3})분)?(?:(\d{1,2})초)$");
        if (match.Success)
        {
            var minutes = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var seconds = int.Parse(match.Groups[2].Value);
            return seconds < 60 ? minutes * 60 + seconds : null;
        }
        match = Regex.Match(text, @"^(\d{1,3}):(\d{2})$");
        if (match.Success && int.Parse(match.Groups[2].Value) < 60)
            return int.Parse(match.Groups[1].Value) * 60 + int.Parse(match.Groups[2].Value);
        // A missing seconds suffix is common at this font size. Never treat
        // a three-digit collapsed minute/second reading as seconds.
        return Regex.IsMatch(text, @"^\d{1,2}$") && int.Parse(text) < 60 ? int.Parse(text) : null;
    }

    private static Bitmap PrepareText(Bitmap bitmap, int scale, bool isolateText = true, bool adaptive = false, bool redOnly = false)
    {
        if (!isolateText)
        {
            var resized = new Bitmap(bitmap.Width * scale, bitmap.Height * scale, PixelFormat.Format24bppRgb);
            using var resizeGraphics = Graphics.FromImage(resized);
            resizeGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            resizeGraphics.DrawImage(bitmap, 0, 0, resized.Width, resized.Height);
            return resized;
        }
        using var mask = adaptive ? TextContrast.CreateMask(bitmap, redOnly) : new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
        if (!adaptive)
        {
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var white = color.R >= 175 && color.G >= 175 && color.B >= 175;
            var red = color.R >= 160 && color.R > color.G * 1.4 && color.R > color.B * 1.3;
            mask.SetPixel(x, y, white || red ? Color.Black : Color.White);
        }
        }
        var enlarged = new Bitmap(bitmap.Width * scale, bitmap.Height * scale, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(enlarged);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        graphics.DrawImage(mask, new Rectangle(0, 0, enlarged.Width, enlarged.Height), 0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel);
        return enlarged;
    }

    public void Dispose() => _engine?.Dispose();
}

public sealed record OcrWord(string Text, int X, int Y, int Width, int Height);
