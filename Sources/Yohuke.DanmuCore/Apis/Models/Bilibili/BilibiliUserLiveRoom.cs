using Newtonsoft.Json;

namespace Yohuke.DanmuCore.Apis.Models.Bilibili;

/// <summary>用户资料中的直播间关联。</summary>
public class BilibiliUserLiveRoom
{
    /// <summary>直播平台是否已开通直播间；公开用户资料未提供时为空。</summary>
    [JsonProperty("roomStatus")]
    public int? RoomStatus { get; set; }

    /// <summary>直播间 ID；零表示尚未开通。</summary>
    [JsonProperty("roomid")]
    public long RoomId { get; set; }

    /// <summary>直播状态。</summary>
    [JsonProperty("liveStatus")]
    public int LiveStatus { get; set; }
}
