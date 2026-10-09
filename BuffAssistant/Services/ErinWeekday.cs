namespace BuffAssistant.Services;

public static class ErinWeekday
{
    private static readonly string[] Names = { "임볼릭", "알반 에일레르", "벨테인", "알반 헤루인", "루나사", "알반 엘베드", "삼하인" };
    private static readonly string[] Days = { "일", "월", "화", "수", "목", "금", "토" };
    private static readonly string[] Effects = {
        "크리티컬·럭키 피니시 확률 상승\n연주·마법 음악·음악 길들이기 성공률 증가",
        "생산 성공률 1.2배 · 생산품 품질 향상\n생활 스킬 랭크업 보너스 1.1배",
        "던전 아이템 획득 확률 증가 · 던전 형태 변화\n전투 스킬 랭크업 보너스 1.1배",
        "채집 확률 10% 증가 · NPC 골드 구매 5% 할인\n은행 수수료 25% 감소 · 완전수련 경험치 1.1배",
        "인챈트 성공률 1.1배 · 장비 숙련도 1.2배\n마법 스킬 랭크업 보너스 증가",
        "포션 효과 1.5배 · 아르바이트 보상 증가",
        "현실 정오에 나이 증가 및 나이별 AP 획득\n음식·L로드 효과 증가 · 동물 스케치 조건 완화\n연금술 생산 품질·성공률 증가"
    };
    public static (string Label, string Description) At(System.DateTimeOffset instant)
    {
        var kst = instant.ToOffset(System.TimeSpan.FromHours(9));
        var day = (int)kst.DayOfWeek;
        return ($"{Names[day]} · {Days[day]}", $"{kst:yyyy.MM.dd} ({Days[day]}) · KST\n{Effects[day]}");
    }
}
