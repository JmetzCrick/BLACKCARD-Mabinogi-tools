using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BuffAssistant.Services;

namespace BuffAssistant;

public sealed class CalculatorPanel : UserControl
{
    private readonly bool _beads;
    private readonly Dictionary<string, TextBox> _inputs = new();
    private readonly StackPanel _results = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 0) };
    private readonly CheckBox _premium = new() { Content = "프리미엄 플러스 (수수료 4%)", IsChecked = true, Margin = new(0, 10, 0, 10) };
    private bool _ready;
    private bool _syncing;
    private static readonly int[] Discounts = [0, 10, 20, 30, 50, 100];

    public CalculatorPanel(bool beads)
    {
        _beads = beads;
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = beads ? "구구 계산기" : "경매장 수수료 계산기", FontSize = 17, Margin = new(0, 0, 0, 8) });
        body.Children.Add(new TextBlock { Text = beads ? "브리 레흐 · 등장 구슬은 2배로 계산합니다." : "수수료 · 쿠폰값 · 기타 비용을 제외한 분배 금액", Foreground = new SolidColorBrush(Color.FromRgb(152, 163, 184)), FontSize = 11, TextWrapping = TextWrapping.Wrap, MinHeight = 20, Margin = new(0, 0, 0, 12) });
        AddInput(body, "members", "분배 인원", beads ? "6" : "1", "명");
        if (beads)
        {
            AddInput(body, "price", "구슬 개당 가격", "300000", "골드");
            foreach (var gate in new[] { "1", "2", "3" }) AddInput(body, "gate" + gate, gate + "관 등장 수", "0", "개");
            AddInput(body, "total", "총 등장 수", "0", "개");
        }
        else
        {
            AddInput(body, "price", "판매 금액", "0", "골드");
            var quick = new UniformGrid { Columns = 3, Margin = new(0, 2, 0, 6) };
            foreach (var amount in new long[] { 100000000, 10000000, 1000000 })
            {
                var button = new Button { Content = amount == 100000000 ? "+ 1억" : "+ " + (amount / 10000).ToString("N0") + "만", Padding = new(6, 5, 6, 5), Margin = new(2), FontSize = 11 };
                button.Click += (_, _) => { if (TryNumber("price", out var value)) _inputs["price"].Text = (value + amount).ToString("0", CultureInfo.InvariantCulture); };
                quick.Children.Add(button);
            }
            body.Children.Add(quick);
            body.Children.Add(_premium);
            _premium.Checked += (_, _) => Recalculate();
            _premium.Unchecked += (_, _) => Recalculate();
            AddInput(body, "other", "기타 비용", "0", "골드");
            var holyWater = new Button { Content = "무리아스의 성수 제작비 + 80숲", Padding = new(8, 6, 8, 6), FontSize = 11, Margin = new(0, 0, 0, 10) };
            holyWater.Click += (_, _) => { if (TryNumber("other", out var value)) _inputs["other"].Text = (value + 800000).ToString("0", CultureInfo.InvariantCulture); };
            body.Children.Add(holyWater);
            body.Children.Add(new TextBlock { Text = "쿠폰 구입 가격 · 실제 가격에 맞게 수정하세요.", FontSize = 11, Foreground = Brushes.LightSteelBlue, Margin = new(0, 3, 0, 8) });
            foreach (var discount in Discounts.Skip(1)) AddInput(body, "coupon" + discount, discount + "% 할인 쿠폰", "0", "골드");
            body.Children.Add(new TextBlock { Text = "쿠폰 가격은 자동 시세가 아닙니다. 0은 무료로 보유한 쿠폰입니다.", FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue, Margin = new(0, 0, 0, 8) });
        }
        body.Children.Add(_error);
        body.Children.Add(_results);
        var viewer = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(2, 0, 6, 0) };
        viewer.Resources.Add(typeof(ScrollBar), Application.Current.FindResource("CalculatorScrollBar"));
        Content = viewer;
        _ready = true;
        Recalculate();
    }

    private void AddInput(StackPanel body, string key, string label, string initial, string unit)
    {
        var row = new Grid { Margin = new(0, 0, 0, 7), MinHeight = 32 };
        row.ColumnDefinitions.Add(new() { Width = new(130) });
        row.ColumnDefinitions.Add(new());
        row.ColumnDefinitions.Add(new() { Width = new(38) });
        row.Children.Add(new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = initial, FontSize = 12, Padding = new(9, 7, 9, 7), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Right };
        box.AutomationId(key);
        _inputs.Add(key, box);
        var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(11, 16, 40)), CornerRadius = new(10), Child = box };
        Grid.SetColumn(surface, 1);
        row.Children.Add(surface);
        var suffix = new TextBlock { Text = unit, FontSize = 10, Foreground = Brushes.LightSteelBlue, VerticalAlignment = VerticalAlignment.Center, Margin = new(6, 0, 0, 0) };
        Grid.SetColumn(suffix, 2);
        row.Children.Add(suffix);
        body.Children.Add(row);
        box.TextChanged += (_, _) => InputChanged(key);
    }

    private bool TryNumber(string key, out decimal number) => decimal.TryParse(_inputs[key].Text.Replace(",", "").Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number) && number <= 1000000000000m;
    private decimal Number(string key) => TryNumber(key, out var value) ? value : throw new ArgumentException("숫자를 입력하세요. (최대 1조)");
    private void InputChanged(string key)
    {
        if (!_ready || _syncing) return;
        if (_beads && (key == "total" || key.StartsWith("gate")))
        {
            _syncing = true;
            try
            {
                if (key == "total") foreach (var gate in new[] { "gate1", "gate2", "gate3" }) _inputs[gate].Text = "0";
                else if (TryNumber("gate1", out var first) && TryNumber("gate2", out var second) && TryNumber("gate3", out var third)) _inputs["total"].Text = (first + second + third).ToString("0.################", CultureInfo.InvariantCulture);
            }
            finally { _syncing = false; }
        }
        Recalculate();
    }

    private void Recalculate()
    {
        if (!_ready) return;
        _results.Children.Clear();
        _error.Text = "";
        try
        {
            var people = Number("members");
            if (people != decimal.Truncate(people) || people is < 1 or > 100) throw new ArgumentException("분배 인원은 1~100명의 정수로 입력하세요.");
            if (_beads)
            {
                foreach (var gate in new[] { "gate1", "gate2", "gate3" }) if (Number(gate) != decimal.Truncate(Number(gate))) throw new ArgumentException("등장 수는 정수로 입력하세요.");
                var result = CalculatorService.Beads(Number("total"), Number("price"), (int)people);
                AddResult("구매 구슬 " + result.Count.ToString("N0") + "개", $"총 {result.Total:N0} 골드\n1인당 {result.Share:N0} 골드\n1숲 단위 올림 {result.RoundedShare:N0} 골드 ({result.RoundedShare / 10000:N0}숲)", result.RoundedShare);
            }
            else
            {
                var rows = Discounts.Select(discount => (Discount: discount, Result: CalculatorService.Auction(Number("price"), _premium.IsChecked == true, discount, discount == 0 ? 0 : Number("coupon" + discount), Number("other"), (int)people))).ToArray();
                var best = rows.Max(row => row.Result.Share);
                foreach (var row in rows)
                    AddResult((row.Discount == 0 ? "쿠폰 없음" : row.Discount + "% 할인 쿠폰") + (row.Result.Share == best && Number("price") > 0 ? " · 최대 수령" : ""), $"수수료 {row.Result.Fee:N0} · 총 비용 {row.Result.Cost:N0}\n1인당 {row.Result.Share:N0} 골드\n수표 분배 (1숲 버림) {row.Result.CheckShare:N0} 골드", row.Result.CheckShare);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException) { _error.Text = ex.Message; }
    }

    private void AddResult(string title, string details, decimal copyAmount)
    {
        var stack = new StackPanel();
        var heading = new Grid();
        heading.Children.Add(new TextBlock { Text = title, FontSize = 12, Foreground = Brushes.LightSteelBlue, VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button { Content = "복사", FontSize = 10, Padding = new(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "수표에 사용할 골드 금액 복사" };
        copy.Click += (_, _) => { try { Clipboard.SetText(copyAmount.ToString("0", CultureInfo.InvariantCulture)); } catch (System.Runtime.InteropServices.COMException) { _error.Text = "클립보드를 사용할 수 없습니다. 다시 눌러주세요."; } };
        heading.Children.Add(copy);
        stack.Children.Add(heading);
        stack.Children.Add(new TextBlock { Text = details, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 0), LineHeight = 19 });
        _results.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(11, 16, 40)), CornerRadius = new(12), Padding = new(12), Margin = new(0, 8, 0, 0), Child = stack });
    }
}

internal static class CalculatorAutomation
{
    public static void AutomationId(this TextBox box, string id) => System.Windows.Automation.AutomationProperties.SetAutomationId(box, "Calculator." + id);
}
