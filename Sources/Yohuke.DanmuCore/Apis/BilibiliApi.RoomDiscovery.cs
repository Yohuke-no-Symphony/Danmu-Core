using Newtonsoft.Json.Linq;
using Yohuke.DanmuCore.Apis.Models.Bilibili;
using Yohuke.DanmuCore.Apis.Models.Common;
using Yohuke.DanmuCore.Errors;

namespace Yohuke.DanmuCore.Apis;

/// <summary>直播间发现使用的公开 UID 关联和搜索设备初始化。</summary>
public partial class BilibiliApi
{
    /// <summary>通过直播平台读取 UID 对应的直播间；不发送客户端登录凭据。</summary>
    public Task<ApiResponse<BilibiliUserLiveRoom>> GetUserLiveRoomAsync(long uid, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uid);
        return GetJsonAsync<ApiResponse<BilibiliUserLiveRoom>>(
            "https://api.live.bilibili.com/room/v1/Room/getRoomInfoOld",
            new Dictionary<string, object?> { ["mid"] = uid },
            CreateLiveHeaders(), timeout, cancellationToken);
    }

    private async Task<string> EnsureSearchDeviceCookieAsync(string cookie, IReadOnlyDictionary<string, string> headers,
        TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ApiCookieHelper.GetValue(cookie, "buvid3")))
        {
            return cookie;
        }

        var response = await GetJsonAsync<ApiResponse<JToken>>(
            "https://api.bilibili.com/x/frontend/finger/spi", headers: headers, timeout: timeout, cancellationToken: cancellationToken);
        var buvid3 = response.Data?["b_3"]?.Value<string>();
        if (response.Code != 0 || string.IsNullOrWhiteSpace(buvid3))
        {
            throw CoreExceptionInfo.Create(CoreErrorCode.RoomSearchFailed, message => new InvalidOperationException(message));
        }

        var values = ApiCookieHelper.Parse(cookie);
        values["buvid3"] = buvid3;
        var buvid4 = response.Data?["b_4"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(ApiCookieHelper.GetValue(cookie, "buvid4")) && !string.IsNullOrWhiteSpace(buvid4))
        {
            values["buvid4"] = buvid4;
        }
        return ApiCookieHelper.CreateBilibiliCookie(string.Join(";", values.Select(pair => $"{pair.Key}={pair.Value}")));
    }
}
