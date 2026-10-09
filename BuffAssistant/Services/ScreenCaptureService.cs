using System;
using System.Drawing;
using System.Drawing.Imaging;
using BuffAssistant.Models;

namespace BuffAssistant.Services;

public sealed class ScreenCaptureService
{
    public Bitmap Capture(ScreenRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            throw new ArgumentException("캡처 영역의 너비와 높이는 0보다 커야 합니다.");

        var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, new Size(rect.Width, rect.Height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
