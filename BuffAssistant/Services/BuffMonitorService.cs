// 공개 배포에서는 사용하지 않습니다. 기존 음악버프 OCR 구현은 주석으로 보존합니다.
// using System;
// using System.Linq;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Drawing;
// using BuffAssistant.Models;
// 
// namespace BuffAssistant.Services;
// 
// public sealed class BuffMonitorService
// {
//     private readonly ScreenCaptureService _capture;
//     private readonly OcrService _ocr;
//     private readonly ColorDetectionService _color;
//     private readonly Action<MonitorEvent> _notify;
// 
//     public BuffMonitorService(ScreenCaptureService capture, OcrService ocr,
//         ColorDetectionService color, Action<MonitorEvent> notify)
//     {
//         _capture = capture;
//         _ocr = ocr;
//         _color = color;
//         _notify = notify;
//     }
// 
//     public async Task RunAsync(ScreenRect region, IReadOnlyList<BuffRule> rules, CancellationToken token)
//     {
//         while (!token.IsCancellationRequested)
//         {
//             using var image = _capture.Capture(region);
//             var words = _ocr.ReadWords(image);
// 
//             ProcessFrame(image, words, rules);
// 
//             await Task.Delay(120, token);
//         }
//     }
// 
//     public void ProcessFrame(Bitmap image, IReadOnlyList<OcrWord> words, IReadOnlyList<BuffRule> rules, DateTimeOffset? observedAt = null)
//     {
//             var foundTimer = false;
//             foreach (var rule in rules)
//             {
//                 var match = FindMatchingWord(words, rule.Name);
// 
//                 if (match is null)
//                 {
//                     rule.ObserveRemainingTime(null, observedAt);
//                     continue;
//                 }
// 
//                 var timerRegion = GetTimerRegion(image.Width, image.Height, match);
//                 var seconds = _ocr.ReadRemainingSeconds(image, timerRegion, out var rawText);
//                 if (seconds == 30 && !_ocr.IsConfirmedThirtySeconds(image, timerRegion, rawText))
//                     seconds = null;
//                 // White/red is a one-minute visual cue. The notification is
//                 // controlled by the actual timer, not the buff-name color.
//                 if (seconds is not null)
//                 {
//                     foundTimer = true;
//                     _notify(new MonitorEvent($"{rule.Name}: 남은 시간 {seconds}초", null));
//                 }
// 
//                 if (rule.ObserveRemainingTime(seconds, observedAt))
//                 {
//                     var audio = string.IsNullOrWhiteSpace(rule.AudioFile) ? null : rule.AudioFile;
//                     _notify(new MonitorEvent(
//                         $"{rule.Name}: 남은 시간 {seconds}초 — 30초 알림",
//                         audio));
//                 }
//             }
//             if (!foundTimer) _notify(new MonitorEvent("버프명·시간을 확인하세요", null));
//     }
// 
//     public static Rectangle GetTimerRegion(int width, int height, OcrWord name)
//     {
//         var left = Math.Max(width / 2, name.X + name.Width + 8);
//         var top = Math.Max(0, name.Y - 4);
//         var bottom = Math.Min(height, name.Y + name.Height + 4);
//         return new Rectangle(Math.Min(left, width), top, Math.Max(0, width - left), Math.Max(0, bottom - top));
//     }
// 
//     private static string Normalize(string text) =>
//         new(text.Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '：').ToArray());
// 
//     public static OcrWord? FindMatchingWord(IReadOnlyList<OcrWord> words, string name)
//     {
//         var target = Normalize(name);
//         if (target.Length == 0) return null;
//         for (var start = 0; start < words.Count; start++)
//         {
//             var first = words[start];
//             var text = "";
//             var right = first.X;
//             var top = first.Y;
//             var bottom = first.Y + first.Height;
//             for (var end = start; end < words.Count; end++)
//             {
//                 var word = words[end];
//                 if (end > start)
//                 {
//                     var overlap = Math.Min(bottom, word.Y + word.Height) - Math.Max(top, word.Y);
//                     if (overlap <= 0 || word.X < words[end - 1].X || word.X - right > Math.Max(first.Height, word.Height) * 2) break;
//                 }
//                 text += Normalize(word.Text);
//                 right = Math.Max(right, word.X + word.Width);
//                 top = Math.Min(top, word.Y);
//                 bottom = Math.Max(bottom, word.Y + word.Height);
//                 if (text.Contains(target, StringComparison.OrdinalIgnoreCase))
//                     return new OcrWord(text, first.X, top, right - first.X, bottom - top);
//                 if (text.Length >= target.Length) break;
//             }
//         }
//         return null;
//     }
// }
