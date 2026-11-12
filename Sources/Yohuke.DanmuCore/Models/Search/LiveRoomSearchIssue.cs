using Yohuke.DanmuCore.Errors;

namespace Yohuke.DanmuCore.Models.Search;

/// <summary>某一查找来源的失败，可与其他来源的成功结果一起返回。</summary>
public sealed class LiveRoomSearchIssue
{
    /// <summary>失败来源。</summary>
    public LiveRoomSearchSource Source { get; init; }

    /// <summary>服务端业务错误码，传输失败时为空。</summary>
    public int? ApiCode { get; init; }

    /// <summary>供宿主按当前语言呈现的本地错误。</summary>
    public CoreErrorInfo? Error { get; init; }

    /// <summary>已脱敏的远端原文或传输诊断，不包含凭据。</summary>
    public string Message { get; init; } = "";
}
