// 공개 배포에서는 사용하지 않습니다. 기존 음악버프 OCR 구현은 주석으로 보존합니다.
// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using BuffAssistant.Models;
// using BuffAssistant.Services;
// 
// internal static class DetectionTests
// {
//     public static void Run(Action<bool, string> check)
//     {
//         check(OcrService.ParseRemainingSeconds("30초") == 30 && OcrService.ParseRemainingSeconds("1분 49초") == 109, "Timer parses seconds and minutes");
//         check(OcrService.ParseRemainingSeconds("1:00") == 60 && OcrService.ParseRemainingSeconds("149") == null && OcrService.ParseRemainingSeconds("4일6시간34분") == null, "Ambiguous and long-duration timers are rejected");
//         var rule = new BuffRule { Name = "전장의 서곡", AudioFile = "default-alert.mp3" };
//         var start = DateTimeOffset.UtcNow;
//         check(!rule.ObserveRemainingTime(60, start) && !rule.ObserveRemainingTime(44, start.AddSeconds(16)) && !rule.ObserveRemainingTime(31, start.AddSeconds(29)), "One-minute color change and 44/31 seconds do not alert");
//         check(!rule.ObserveRemainingTime(30, start.AddSeconds(30)) && rule.ObserveRemainingTime(30, start.AddSeconds(30.2)), "Two exact thirty-second readings alert once while timer still says thirty");
//         check(!rule.ObserveRemainingTime(29, start.AddSeconds(31)) && !rule.ObserveRemainingTime(null, start.AddSeconds(31.2)) && !rule.ObserveRemainingTime(28, start.AddSeconds(32)), "OCR miss and countdown do not repeat alert");
//         check(!rule.ObserveRemainingTime(44, start.AddSeconds(32.2)) && !rule.ObserveRemainingTime(26, start.AddSeconds(34)), "One erroneous high timer does not rearm alert");
//         check(!rule.ObserveRemainingTime(90, start.AddSeconds(35)) && !rule.ObserveRemainingTime(89, start.AddSeconds(35.2)) && !rule.ObserveRemainingTime(89, start.AddSeconds(35.4)) && !rule.ObserveRemainingTime(30, start.AddSeconds(94.4)) && rule.ObserveRemainingTime(30, start.AddSeconds(94.6)), "Confirmed buff extension rearms next exact thirty-second alert");
//         var sudden = new BuffRule();
//         check(!sudden.ObserveRemainingTime(44, start) && !sudden.ObserveRemainingTime(30, start.AddSeconds(0.2)) && !sudden.ObserveRemainingTime(30, start.AddSeconds(0.4)), "Impossible OCR drop 44 to 30 is rejected even if repeated");
//         var below = new BuffRule();
//         check(!below.ObserveRemainingTime(29) && !below.ObserveRemainingTime(29) && !below.ObserveRemainingTime(5) && !below.ObserveRemainingTime(0), "Starting detection below thirty seconds never triggers a late alert");
//         var interrupted = new BuffRule();
//         check(!interrupted.ObserveRemainingTime(30, start) && !interrupted.ObserveRemainingTime(null, start.AddSeconds(0.2)) && interrupted.ObserveRemainingTime(30, start.AddSeconds(0.4)), "Brief background-induced OCR miss preserves recent exact-thirty confirmation");
// 
//         using var ocr = new OcrService();
//         var events = new List<MonitorEvent>();
//         var monitor = new BuffMonitorService(new ScreenCaptureService(), ocr, new ColorDetectionService(), events.Add);
//         rule.ResetAlert();
//         foreach (var sample in new[] { ("Buff44Seconds.png", 44), ("Buff109Seconds.png", 109) })
//         {
//             using var image = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Fixtures", sample.Item1));
//             var words = ocr.ReadWords(image);
//             var name = BuffMonitorService.FindMatchingWord(words, rule.Name);
//             check(name != null, "Game screenshot buff name recognized: " + sample.Item1);
//             var region = BuffMonitorService.GetTimerRegion(image.Width, image.Height, name!);
//             check(ocr.ReadRemainingSeconds(image, region) == sample.Item2, "Game screenshot timer recognized: " + sample.Item2 + " seconds");
//             monitor.ProcessFrame(image, words, new[] { rule });
//             monitor.ProcessFrame(image, words, new[] { rule });
//         }
//         check(events.All(e => e.AudioFile == null), "Actual 44-second and 109-second screenshots produce no audio");
// 
//         // Render a controlled timer fixture while using the original name and row
//         // coordinates. This exercises timer OCR, row association and audio events.
//         using var thirty = CreateTimerFixture("30초");
//         var fixtureWords = new[] { new OcrWord("전장의 서곡", 30, 14, 53, 11) };
//         check(ocr.ReadRemainingSeconds(thirty, BuffMonitorService.GetTimerRegion(thirty.Width, thirty.Height, fixtureWords[0])) == 30, "Rendered red thirty-second timer recognized");
//         var exactRegion = BuffMonitorService.GetTimerRegion(thirty.Width, thirty.Height, fixtureWords[0]);
//         foreach (var background in new[] { Color.FromArgb(60, 65, 48), Color.FromArgb(170, 185, 175), Color.FromArgb(95, 100, 130) })
//         {
//             using var changing = CreateTimerFixture("30초", Color.FromArgb(145, 55, 55), background);
//             var changingSeconds = ocr.ReadRemainingSeconds(changing, exactRegion, out var changingText);
//             check(changingSeconds == 30 && ocr.IsConfirmedThirtySeconds(changing, exactRegion, changingText), "Dim red timer recognized against changed background: " + background);
//         }
//         check(!ocr.IsConfirmedThirtySeconds(thirty, exactRegion, "30") && !ocr.IsConfirmedThirtySeconds(thirty, exactRegion, "1분30초"), "Missing units and minute timers cannot pass thirty-second confirmation");
//         using var whiteThirty = CreateTimerFixture("30초", Color.White);
//         check(!ocr.IsConfirmedThirtySeconds(whiteThirty, exactRegion, "30초"), "White minute timer artifacts cannot confirm thirty-second alert");
//         using var twentyNine = CreateTimerFixture("29초");
//         var beforeLate = events.Count(e => e.AudioFile != null);
//         var lateRule = new BuffRule { Name = rule.Name, AudioFile = rule.AudioFile };
//         monitor.ProcessFrame(twentyNine, fixtureWords, new[] { lateRule });
//         monitor.ProcessFrame(twentyNine, fixtureWords, new[] { lateRule });
//         check(events.Count(e => e.AudioFile != null) == beforeLate, "Actual OCR of twenty-nine-second timer produces no late audio");
//         rule.ResetAlert();
//         monitor.ProcessFrame(thirty, fixtureWords, new[] { rule }, start);
//         check(events.Count(e => e.AudioFile == rule.AudioFile) == 0, "Single OCR thirty-second frame cannot trigger audio");
//         monitor.ProcessFrame(thirty, fixtureWords, new[] { rule }, start.AddSeconds(0.2));
//         monitor.ProcessFrame(thirty, fixtureWords, new[] { rule }, start.AddSeconds(0.4));
//         check(events.Count(e => e.AudioFile == rule.AudioFile) == 1, "Thirty-second frame produces exactly one configured audio event");
//         using var extended = CreateTimerFixture("1분 49초", Color.White);
//         var extendedSeconds = ocr.ReadRemainingSeconds(extended, BuffMonitorService.GetTimerRegion(extended.Width, extended.Height, fixtureWords[0]), out var extendedText);
//         check(extendedSeconds == 109, $"White minute timer remains above thirty seconds ({extendedText} => {extendedSeconds})");
//         monitor.ProcessFrame(extended, fixtureWords, new[] { rule }, start.AddSeconds(3));
//         monitor.ProcessFrame(extended, fixtureWords, new[] { rule }, start.AddSeconds(3.2));
//         monitor.ProcessFrame(extended, fixtureWords, new[] { rule }, start.AddSeconds(3.4));
//         monitor.ProcessFrame(thirty, fixtureWords, new[] { rule }, start.AddSeconds(82.4));
//         monitor.ProcessFrame(thirty, fixtureWords, new[] { rule }, start.AddSeconds(82.6));
//         check(events.Count(e => e.AudioFile == rule.AudioFile) == 2, "Timer extension enables a second audio event on next thirty seconds");
//     }
// 
//     private static Bitmap CreateTimerFixture(string text, Color? color = null, Color? background = null)
//     {
//         var bitmap = new Bitmap(297, 80);
//         using var graphics = Graphics.FromImage(bitmap);
//         graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
//         using var font = new Font("Malgun Gothic", 12, FontStyle.Regular, GraphicsUnit.Pixel);
//         using var brush = new SolidBrush(color ?? Color.Red);
//         graphics.Clear(background ?? Color.FromArgb(41, 51, 21));
//         graphics.DrawString(text, font, brush, 225, 11);
//         // A different buff's timer on the next row must not be read.
//         graphics.DrawString("4일 6시간 34분", font, System.Drawing.Brushes.White, 160, 36);
//         return bitmap;
//     }
// }
