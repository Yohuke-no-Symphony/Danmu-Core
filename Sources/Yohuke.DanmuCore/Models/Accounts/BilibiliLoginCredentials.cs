namespace Yohuke.DanmuCore.Models.Accounts;

/// <summary>后台保管的登录凭据与刷新确认状态。</summary>
public sealed record BilibiliLoginCredentials
{
    public string Cookie { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public string PendingConfirmationToken { get; init; } = "";
}
