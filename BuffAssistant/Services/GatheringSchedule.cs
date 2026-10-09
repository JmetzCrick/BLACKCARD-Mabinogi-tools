namespace BuffAssistant.Services;

/// <summary>PC official mobile homepage /m/main/index.asp, erinn_time():
/// round((seconds since midnight % 2160) - 24), 90 real seconds/hour.
/// Normalize midnight's negative remainder; KST midnight and Unix epoch have the same cycle phase.
/// </summary>
public static class ErinClock
{
    public const long DayMilliseconds = 2_160_000;
    public const long OffsetMilliseconds = 24_000;
    public static long OfficialSecond(DateTimeOffset time) => (long)Math.Floor((time.ToUnixTimeMilliseconds() - OffsetMilliseconds) / 1000.0 + 0.5);
    public static int Minute(DateTimeOffset time) => (int)(PositiveRemainder(OfficialSecond(time), 2160) / 1.5);
    public static string Display(DateTimeOffset time) { var minute = Minute(time); return $"{minute / 60:00}:{minute % 60:00}"; }
    public static long PositiveRemainder(long value, long modulus) => (value % modulus + modulus) % modulus;
    public static DateTimeOffset Boundary(long day, int hour) => DateTimeOffset.FromUnixTimeMilliseconds(day * DayMilliseconds + hour * 90_000 + OffsetMilliseconds - 500);
    public static TimeSpan Until(DateTimeOffset now, int hour)
    {
        var day = (long)Math.Floor(OfficialSecond(now) / 2160.0);
        var target = Boundary(day, hour);
        if (target <= now) target = Boundary(day + 1, hour);
        return target - now;
    }
}

public sealed record GatheringItem(string Name, int StartHour, int EndHour, string Skill)
{
    public string TimeRange => $"{StartHour:00}:00–{EndHour:00}:00";
    public bool Available(DateTimeOffset now)
    {
        var minute = ErinClock.Minute(now);
        return StartHour < EndHour ? minute >= StartHour * 60 && minute < EndHour * 60
            : minute >= StartHour * 60 || minute < EndHour * 60;
    }
}

public static class GatheringSchedule
{
    // The 19 rare gathering windows supplied by the user; all times are Erin time.
    public static IReadOnlyList<GatheringItem> Items { get; } = new GatheringItem[]
    {
        new("노랑망태버섯",0,6,"채집 · 버섯 채집"), new("밀키쿼츠",1,7,"광업 · 야금술"),
        new("설련화",2,8,"채집 · 약초학"), new("남동석",4,10,"광업 · 채광"),
        new("브리움 우유",5,6,"목축 · 우유 짜기"), new("악마의 손가락",6,12,"채집 · 버섯 채집"),
        new("카넬리안",7,13,"광업 · 야금술"), new("산딸기",8,14,"채집 · 약초학"),
        new("여울 이삭",9,15,"농사 · 추수"), new("적철석",10,16,"광업 · 채광"),
        new("아벤츄린",13,19,"광업 · 야금술"), new("신비한 깃털",13,14,"목축 · 달걀 채집"),
        new("루멘 플랜트",14,20,"채집 · 약초학"), new("백연석",16,22,"광업 · 채광"),
        new("힐웬 광정",16,17,"광업 · 희귀 광물학"), new("마력 심재핵",18,24,"수공예 · 목공"),
        new("실리엔 응축핵",20,21,"채집 · 실리엔 생태학"), new("빛나는 양털",21,22,"목축 · 양털 깎기"),
        new("월광 당근",23,5,"농사 · 호미질")
    };
}

/// <summary>Only start crossings can alert. Launch, toggle, backward clock changes and sleep never replay old starts.</summary>
public sealed class GatheringAlarmTracker
{
    private DateTimeOffset? _previous;
    private readonly Dictionary<string, long> _lastDay = new();
    public void Reset(DateTimeOffset now) => _previous = now;
    public IReadOnlyList<GatheringItem> Tick(DateTimeOffset now, bool enabled, IReadOnlySet<string> selected)
    {
        var previous = _previous;
        _previous = now;
        if (!enabled || previous is null || now <= previous || now - previous > TimeSpan.FromSeconds(5)) return Array.Empty<GatheringItem>();
        var day = (long)Math.Floor(ErinClock.OfficialSecond(now) / 2160.0);
        var alarms = new List<GatheringItem>();
        foreach (var item in GatheringSchedule.Items.Where(x => selected.Contains(x.Name)))
        {
            var start = ErinClock.Boundary(day, item.StartHour);
            if (start > previous && start <= now && (!_lastDay.TryGetValue(item.Name, out var last) || day > last))
            { _lastDay[item.Name] = day; alarms.Add(item); }
        }
        return alarms;
    }
}
