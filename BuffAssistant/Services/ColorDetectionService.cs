using System;
using System.Drawing;

namespace BuffAssistant.Services;

public sealed class ColorDetectionService
{
    /// <summary>
    /// Measures red-dominant pixels inside an OCR word's bounding box.
    /// This is a heuristic; calibrate thresholds for the target game's UI.
    /// </summary>
    public double GetRedRatio(Bitmap image, int x, int y, int width, int height)
    {
        x = Math.Clamp(x, 0, image.Width - 1);
        y = Math.Clamp(y, 0, image.Height - 1);
        width = Math.Clamp(width, 1, image.Width - x);
        height = Math.Clamp(height, 1, image.Height - y);

        long redCount = 0;
        long total = 0;
        for (int py = y; py < y + height; py += 1)
        for (int px = x; px < x + width; px += 1)
        {
            var c = image.GetPixel(px, py);
            // Red-dominant rule, ignoring near-gray anti-aliasing pixels.
            if (c.R > 145 && c.R > c.G * 1.35 && c.R > c.B * 1.25)
                redCount++;
            total++;
        }
        return total == 0 ? 0 : (double)redCount / total;
    }
}
