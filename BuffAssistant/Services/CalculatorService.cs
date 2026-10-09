namespace BuffAssistant.Services;

public static class CalculatorService
{
    public static BeadResult Beads(decimal appearances, decimal unitPrice, int members)
    {
        if (appearances < 0 || appearances != decimal.Truncate(appearances) || unitPrice < 0 || members < 1)
            throw new ArgumentException("등장 수와 가격은 0 이상, 분배 인원은 1명 이상으로 입력하세요.");
        var count = appearances * 2;
        var total = count * unitPrice;
        return new(count, total, decimal.Round(total / members, 0, MidpointRounding.AwayFromZero), decimal.Ceiling(total / members / 10000) * 10000);
    }

    public static FeeResult Auction(decimal price, bool premium, int discount, decimal couponPrice, decimal otherCost, int members)
    {
        if (price < 0 || couponPrice < 0 || otherCost < 0 || members < 1 || discount is < 0 or > 100)
            throw new ArgumentException("금액은 0 이상, 분배 인원은 1명 이상으로 입력하세요.");
        var fee = decimal.Round(price * (premium ? 0.04m : 0.05m) * (100 - discount) / 100, 0, MidpointRounding.AwayFromZero);
        var cost = fee + (discount == 0 ? 0 : couponPrice) + otherCost;
        var share = (price - cost) / members;
        return new(fee, cost, decimal.Round(share, 0, MidpointRounding.AwayFromZero), decimal.Floor(share / 10000) * 10000);
    }
}

public record BeadResult(decimal Count, decimal Total, decimal Share, decimal RoundedShare);
public record FeeResult(decimal Fee, decimal Cost, decimal Share, decimal CheckShare);
