using System;
using System.Collections.Generic;
using System.Linq;
using BuffAssistant.Services;

internal static class GatheringTests
{
    public static void Run(Action<bool, string> check)
    {
        const long day = 20000;
        check(BundledAuctionKey.Read().Length > 20 && typeof(BundledAuctionKey).Assembly.GetManifestResourceNames().Contains("BlackCardHelper.AuctionKey"), "Auction credential is embedded and requires no external key file or account");
        check(GatheringSchedule.Items.Count == 19, "All 19 supplied gathering schedules are present");
        var settings = new BuffAssistant.Models.AppSettings { GatheringAlarmsEnabled = true, GatheringAlarmItems = new() { "월광 당근" }, GatheringVolumePercent = 35 };
        var restored = System.Text.Json.JsonSerializer.Deserialize<BuffAssistant.Models.AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings));
        check(restored.GatheringAlarmsEnabled && restored.GatheringAlarmItems.Single() == "월광 당근" && restored.GatheringVolumePercent == 35, "Gathering selections, master switch and independent volume survive settings serialization");
        for (int minute = 0; minute < 1440; minute++)
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(day * ErinClock.DayMilliseconds + 24000 + minute * 1500);
            if (ErinClock.Minute(now) != minute) throw new Exception("Official clock minute conversion");
        }
        check(true, "All Erin minutes match official 36-minute cycle and 24-second correction");
        var zero = ErinClock.Boundary(day, 0);
        check(ErinClock.Display(zero.AddMilliseconds(-1)) == "23:59" && ErinClock.Display(zero) == "00:00", "Official rounding boundary normalizes midnight correctly");
        var carrot = GatheringSchedule.Items.Single(x => x.Name == "월광 당근");
        check(carrot.Available(ErinClock.Boundary(day,23)) && carrot.Available(zero) && !carrot.Available(ErinClock.Boundary(day,5)), "Overnight gathering window includes midnight and excludes end time");
        var milk = GatheringSchedule.Items.Single(x => x.Name == "브리움 우유");
        check(milk.Available(ErinClock.Boundary(day,5)) && !milk.Available(ErinClock.Boundary(day,6)), "One-hour gathering window has exact start and end");
        var tracker = new GatheringAlarmTracker();
        var start = ErinClock.Boundary(day,5);
        var selected = new HashSet<string> { milk.Name };
        check(tracker.Tick(start, true, selected).Count == 0, "Launching at a gathering start never replays an alarm");
        tracker.Reset(start.AddMilliseconds(-250));
        check(tracker.Tick(start, true, selected).Count == 1 && tracker.Tick(start.AddMilliseconds(250), true, selected).Count == 0, "Selected gathering start alerts exactly once");
        tracker.Reset(start.AddMilliseconds(-250));
        check(tracker.Tick(start, true, selected).Count == 0, "Clock adjustment never duplicates the same gathering day");
        var next = start.AddMilliseconds(ErinClock.DayMilliseconds);
        tracker.Reset(next.AddMilliseconds(-250));
        check(tracker.Tick(next, true, selected).Count == 1, "Alarm repeats on the next Erin day");
        tracker.Reset(next.AddMilliseconds(ErinClock.DayMilliseconds - 250));
        check(tracker.Tick(next.AddMilliseconds(ErinClock.DayMilliseconds), false, selected).Count == 0, "Master off suppresses gathering alerts");
        tracker.Reset(next.AddMilliseconds(ErinClock.DayMilliseconds - 250));
        check(tracker.Tick(next.AddMilliseconds(ErinClock.DayMilliseconds), true, new HashSet<string>()).Count == 0, "Unchecked gathering items never alert");
        tracker.Reset(start.AddSeconds(-10));
        check(tracker.Tick(start, true, selected).Count == 0, "Sleep or long dispatch delays do not replay stale gathering alerts");
        tracker.Reset(zero.AddMilliseconds(-250));
        check(tracker.Tick(zero, true, new HashSet<string> { "노랑망태버섯" }).Count == 1, "Gathering starts across Erin midnight alert correctly");
        tracker = new GatheringAlarmTracker();
        var thirteen = ErinClock.Boundary(day,13);
        tracker.Reset(thirteen.AddMilliseconds(-250));
        check(tracker.Tick(thirteen, true, new HashSet<string> { "아벤츄린", "신비한 깃털" }).Count == 2, "Simultaneous gathering starts are batched into one sound notification");
    }
}
