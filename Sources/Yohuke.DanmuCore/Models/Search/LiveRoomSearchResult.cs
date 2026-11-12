namespace Yohuke.DanmuCore.Models.Search;

/// <summary>可添加到工作区的直播间资料，不包含界面状态。</summary>
public sealed class LiveRoomSearchResult
{
    /// <summary>公开直播间 ID。</summary>
    public string RoomId { get; init; } = "";

    /// <summary>服务器提供的短房间号。</summary>
    public string ShortRoomId { get; init; } = "";

    /// <summary>合并到当前房间的查找来源。</summary>
    public LiveRoomSearchSource Sources { get; set; } = LiveRoomSearchSource.Nickname;

    /// <summary>主播 UID。</summary>
    public string OwnerUid { get; init; } = "";

    /// <summary>去除搜索高亮标记的主播昵称。</summary>
    public string OwnerName { get; init; } = "";

    /// <summary>去除搜索高亮标记的直播标题。</summary>
    public string Title { get; init; } = "";

    /// <summary>当前是否正在直播。</summary>
    public bool IsLive { get; init; }
}
