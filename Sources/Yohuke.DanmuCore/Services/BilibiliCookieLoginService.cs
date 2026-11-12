using System.Net;
using Yohuke.DanmuCore.Apis.Models.Bilibili;
using Yohuke.DanmuCore.Errors;

namespace Yohuke.DanmuCore.Services;

/// <summary>从浏览器 Cookie 提取 B 站凭据，不管理浏览器或本地用户资料。</summary>
public sealed class BilibiliCookieLoginService
{
    /// <summary>B 站官方登录页面。</summary>
    public static Uri LoginUri { get; } = new("https://passport.bilibili.com/login");

    /// <summary>提取登录与设备 Cookie，并验证登录所需字段。</summary>
    public BilibiliLoginCookie CreateCookie(IEnumerable<Cookie> cookies)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        var cookie = BilibiliLoginCookie.FromCookies(cookies);
        if (string.IsNullOrWhiteSpace(cookie.SessData) || string.IsNullOrWhiteSpace(cookie.BiliJct))
        {
            throw CoreExceptionInfo.Create(CoreErrorCode.BrowserLoginBilibiliCookiesMissing,
                message => new InvalidOperationException(message));
        }

        return cookie;
    }
}
