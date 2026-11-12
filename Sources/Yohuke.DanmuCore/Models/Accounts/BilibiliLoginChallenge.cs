namespace Yohuke.DanmuCore.Models.Accounts;

/// <summary>平台返回的极验或图片挑战。</summary>
public sealed record BilibiliLoginChallenge(string Type, string Token, string Gt, string Challenge, string? ImageUrl);
