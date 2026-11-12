
namespace Yohuke.DanmuCore.Apis.Models.Bilibili;

/// <summary>携带直播间和服务器时间的红包内容或开奖更新。</summary>
public sealed record BilibiliRedPocketMessage
{
    public string RoomId { get; init; } = "";
    public long Timestamp { get; init; }
    /// <summary>是否为开奖消息，独立于中奖列表是否已经返回。</summary>
    public bool IsDrawn { get; init; }
    public RedPocketData RedPocket { get; init; } = new();
}
