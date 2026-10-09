using BuffAssistant.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Collections.Generic;
using BuffAssistant;

internal static class CalculatorTests
{
    public static void RunUi(MainWindow main, Action<bool, string> check)
    {
        foreach (var beads in new[] { true, false })
        {
            typeof(MainWindow).GetMethod(beads ? "BeadTab_Click" : "FeeTab_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            var panel = (Border)main.FindName(beads ? "BeadPanel" : "FeePanel");
            check(panel.Visibility == Visibility.Visible && ((FrameworkElement)main.FindName("ScheduleSiteButton")).Visibility == Visibility.Visible, "Calculator tab opens with persistent schedule footer");
            var control = (CalculatorPanel)panel.Child;
            var inputs = (Dictionary<string, TextBox>)typeof(CalculatorPanel).GetField("_inputs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
            if (beads)
            {
                inputs["gate1"].Text = "1"; inputs["gate2"].Text = "2"; inputs["gate3"].Text = "3";
                check(inputs["total"].Text == "6", "Editing gates updates total appearances");
                inputs["total"].Text = "1";
                check(inputs["gate1"].Text == "0" && inputs["gate2"].Text == "0" && inputs["gate3"].Text == "0", "Manual total clears gate quantities");
                inputs["price"].Text = "500000";
            }
            else inputs["price"].Text = "666666666";
            var root = (FrameworkElement)main.Content;
            root.Measure(new Size(440, 680)); root.Arrange(new Rect(0, 0, 440, 680)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(440, 680, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = System.IO.File.Create(beads ? "bead-preview.png" : "fee-preview.png"); encoder.Save(file);
            inputs["members"].Text = "0";
            var error = (TextBlock)typeof(CalculatorPanel).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
            check(error.Text.Length > 0, "Invalid party size shows error without stale results");
            inputs["members"].Text = beads ? "6" : "1";
        }
        typeof(MainWindow).GetMethod("BuffTab_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
        check(((FrameworkElement)main.FindName("BeadPanel")).Visibility == Visibility.Collapsed && ((FrameworkElement)main.FindName("FeePanel")).Visibility == Visibility.Collapsed, "Returning to buff tab hides both calculators");
    }
    public static void Run(Action<bool, string> check)
    {
        var beads = CalculatorService.Beads(1, 500000, 6);
        check(beads.Count == 2 && beads.Total == 1000000 && beads.Share == 166667 && beads.RoundedShare == 170000, "Bead reference: doubled count, six-person division and 1-sup ceiling");
        check(CalculatorService.Beads(6, 300000, 6).RoundedShare == 600000, "Gate sum distribution preserves exact 1-sup multiples");
        check(CalculatorService.Beads(0, 300000, 6).Total == 0, "Zero beads yields zero payment");
        var fee = CalculatorService.Auction(666666666, true, 0, 999, 0, 1);
        check(fee.Fee == 26666667 && fee.Share == 639999999 && fee.CheckShare == 639990000, "Premium auction reference and cheque rounding");
        check(CalculatorService.Auction(666666666, true, 10, 340000, 0, 1).Share == 642326666, "10-percent coupon reference subtracts purchase cost");
        check(CalculatorService.Auction(1000000, false, 0, 0, 100000, 2).Share == 425000, "Standard five-percent fee, other costs and party split");
        check(CalculatorService.Auction(1000000, true, 100, 10000, 0, 1).Share == 990000, "Full discount removes fee but retains coupon cost");
        check(CalculatorService.Auction(100, true, 0, 0, 1000, 1).Share < 0, "Loss is shown instead of silently clamped");
        foreach (var invalid in new Action[] { () => CalculatorService.Beads(1.5m, 1, 6), () => CalculatorService.Beads(1, 1, 0), () => CalculatorService.Auction(-1, false, 0, 0, 0, 1) })
        {
            var rejected = false;
            try { invalid(); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid calculator input rejected");
        }
    }
}

