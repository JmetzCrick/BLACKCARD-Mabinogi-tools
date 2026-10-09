using System.IO;

namespace BuffAssistant.Models;

public sealed class BuffRule
{
    public string Name { get; set; } = "";
    public string AudioFile { get; set; } = "";
    public double RedPixelRatioThreshold { get; set; } = 0.12;
    public int RequiredConsecutiveDetections { get; set; } = 2;
    public bool Alerted { get; private set; }
    public int ConsecutiveRedFrames { get; set; }
    private int _consecutiveRenewalFrames;
    private int? _lastTrustedSeconds;
    private DateTimeOffset _lastTrustedAt;
    private int? _renewalCandidateSeconds;
    private DateTimeOffset _renewalCandidateAt;
    private DateTimeOffset _lastThirtyAt;

    public string ThresholdDescription => "정확히 30초";
    public string AudioFileShort => string.IsNullOrWhiteSpace(AudioFile) ? "(미지정)" : Path.GetFileName(AudioFile);

    public bool TryTrigger(bool isRed)
    {
        if (!isRed)
        {
            ConsecutiveRedFrames = 0;
            Alerted = false;
            return false;
        }

        ConsecutiveRedFrames++;
        if (ConsecutiveRedFrames >= RequiredConsecutiveDetections && !Alerted)
        {
            Alerted = true;
            return true;
        }
        return false;
    }

    public void ResetAlert()
    {
        Alerted = false;
        ConsecutiveRedFrames = 0;
        _consecutiveRenewalFrames = 0;
        _lastTrustedSeconds = null;
        _renewalCandidateSeconds = null;
        _lastThirtyAt = default;
    }

    public bool ObserveRemainingTime(int? remainingSeconds, DateTimeOffset? observedAt = null)
    {
        var now = observedAt ?? DateTimeOffset.UtcNow;
        if (remainingSeconds is null || remainingSeconds < 0)
        {
            if ((now - _lastThirtyAt).TotalSeconds > 0.6) ConsecutiveRedFrames = 0;
            _consecutiveRenewalFrames = 0;
            _renewalCandidateSeconds = null;
            return false;
        }
        var seconds = remainingSeconds.Value;
        if (_lastTrustedSeconds is int previous)
        {
            var elapsed = Math.Max(0, (now - _lastTrustedAt).TotalSeconds);
            // Reject instantaneous OCR drops such as 44 -> 30 or 40 -> 3.
            if (previous - seconds > Math.Ceiling(elapsed) + 2)
            {
                ConsecutiveRedFrames = 0;
                return false;
            }
            if (seconds > previous + 2)
            {
                ConsecutiveRedFrames = 0;
                var candidateElapsed = Math.Max(0, (now - _renewalCandidateAt).TotalSeconds);
                var followsCandidate = _renewalCandidateSeconds is int candidate &&
                    seconds <= candidate + 1 && candidate - seconds <= Math.Ceiling(candidateElapsed) + 2;
                _consecutiveRenewalFrames = followsCandidate ? _consecutiveRenewalFrames + 1 : 1;
                _renewalCandidateSeconds = seconds;
                _renewalCandidateAt = now;
                if (_consecutiveRenewalFrames < 3) return false;
                if (seconds > 30) Alerted = false;
            }
        }
        _lastTrustedSeconds = seconds;
        _lastTrustedAt = now;
        _consecutiveRenewalFrames = 0;
        _renewalCandidateSeconds = null;
        if (seconds != 30)
        {
            ConsecutiveRedFrames = 0;
            return false;
        }
        // Require consecutive, recent exact readings; never alert at 29 or below.
        if ((now - _lastThirtyAt).TotalSeconds > 1.2) ConsecutiveRedFrames = 0;
        _lastThirtyAt = now;
        if (++ConsecutiveRedFrames >= 2 && !Alerted)
        {
            Alerted = true;
            return true;
        }
        return false;
    }
}
