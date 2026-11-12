namespace Yohuke.DanmuCore.Models.Accounts;

/// <summary>平台登录结果，可能要求用户继续安全验证。</summary>
public sealed record BilibiliWebLoginResult(string Status, BilibiliLoginCredentials? Credentials = null, string? VerificationUrl = null, string? Message = null);
