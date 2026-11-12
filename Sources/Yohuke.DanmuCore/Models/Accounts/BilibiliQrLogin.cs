namespace Yohuke.DanmuCore.Models.Accounts;

/// <summary>二维码内容与服务端保管的轮询键。</summary>
public sealed record BilibiliQrLogin(string Url, string Key);
