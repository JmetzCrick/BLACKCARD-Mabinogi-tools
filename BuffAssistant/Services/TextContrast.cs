// 공개 배포에서는 사용하지 않습니다. 기존 음악버프 OCR 구현은 주석으로 보존합니다.
// using System;
// using System.Drawing;
// using System.Drawing.Imaging;
// 
// namespace BuffAssistant.Services;
// 
// public static class TextContrast
// {
//     public static Bitmap CreateMask(Bitmap image, bool redOnly = false)
//     {
//         var width = image.Width;
//         var height = image.Height;
//         var integral = new double[width + 1, height + 1];
//         var redIntegral = new double[width + 1, height + 1];
//         for (var y = 0; y < height; y++)
//         for (var x = 0; x < width; x++)
//         {
//             var c = image.GetPixel(x, y);
//             var brightness = (c.R + c.G + c.B) / 3.0;
//             integral[x + 1, y + 1] = brightness + integral[x, y + 1] + integral[x + 1, y] - integral[x, y];
//             var excess = c.R - Math.Max(c.G, c.B);
//             redIntegral[x + 1, y + 1] = excess + redIntegral[x, y + 1] + redIntegral[x + 1, y] - redIntegral[x, y];
//         }
//         var mask = new Bitmap(width, height, PixelFormat.Format24bppRgb);
//         for (var y = 0; y < height; y++)
//         for (var x = 0; x < width; x++)
//         {
//             var left = Math.Max(0, x - 5);
//             var right = Math.Min(width, x + 6);
//             var top = Math.Max(0, y - 5);
//             var bottom = Math.Min(height, y + 6);
//             var average = (integral[right, bottom] - integral[left, bottom] - integral[right, top] + integral[left, top]) / ((right - left) * (bottom - top));
//             var redAverage = (redIntegral[right, bottom] - redIntegral[left, bottom] - redIntegral[right, top] + redIntegral[left, top]) / ((right - left) * (bottom - top));
//             var c = image.GetPixel(x, y);
//             var minimum = Math.Min(c.R, Math.Min(c.G, c.B));
//             var maximum = Math.Max(c.R, Math.Max(c.G, c.B));
//             var white = minimum >= 85 && maximum - minimum < 75 && (c.R + c.G + c.B) / 3.0 > average + 15;
//             var redExcess = c.R - Math.Max(c.G, c.B);
//             var red = redExcess > 32 && redExcess > redAverage + 20;
//             mask.SetPixel(x, y, (!redOnly && white) || red ? Color.Black : Color.White);
//         }
//         return mask;
//     }
// }
