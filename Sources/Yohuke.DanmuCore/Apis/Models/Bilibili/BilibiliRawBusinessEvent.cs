namespace Yohuke.DanmuCore.Apis.Models.Bilibili;

/// <summary>同一弹幕连接解码后的业务包，不包含认证帧。</summary>
public sealed record BilibiliRawBusinessEvent(string RoomId, string Command, string RawJson, DateTimeOffset ReceivedAtUtc);
