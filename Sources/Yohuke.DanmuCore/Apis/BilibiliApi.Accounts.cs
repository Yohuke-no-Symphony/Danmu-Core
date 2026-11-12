using Yohuke.DanmuCore.Apis.Models.Bilibili;
using Yohuke.DanmuCore.Apis.Models.Common;

namespace Yohuke.DanmuCore.Apis;

public partial class BilibiliApi
{
    /// <summary>
    /// 使用指定账号的凭据验证登录身份。
    /// </summary>
    public Task<ApiResponse<BilibiliNavigationData>> GetNavigationAsync(int number = 0,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<ApiResponse<BilibiliNavigationData>>(
            "https://api.bilibili.com/x/web-interface/nav", headers: CreateAccountHeaders(number),
            timeout: timeout, cancellationToken: cancellationToken);
    }
}
