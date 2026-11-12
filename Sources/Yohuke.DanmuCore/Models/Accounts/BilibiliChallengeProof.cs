namespace Yohuke.DanmuCore.Models.Accounts;

/// <summary>用户在平台验证码控件中产生的挑战证明。</summary>
public sealed record BilibiliChallengeProof
{
    public string Token { get; init; } = "";
    public string? Challenge { get; init; }
    public string? Validate { get; init; }
    public string? Seccode { get; init; }
    public string? LotNumber { get; init; }
    public string? CaptchaOutput { get; init; }
    public string? PassToken { get; init; }
    public string? GenTime { get; init; }
    public string? Captcha { get; init; }
}
