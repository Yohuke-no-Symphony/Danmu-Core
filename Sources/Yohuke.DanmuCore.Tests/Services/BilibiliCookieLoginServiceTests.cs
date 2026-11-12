using System.Net;
using Xunit;
using Yohuke.DanmuCore.Errors;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Services;

/// <summary>验证无界面浏览器凭据提取与错误语义。</summary>
public sealed class BilibiliCookieLoginServiceTests
{
    /// <summary>登录字段和设备 Cookie 来自 B 站域名，第三方同名字段不覆盖它们。</summary>
    [Fact]
    public void ExtractsOnlyBilibiliCredentialsAndDeviceCookies()
    {
        var cookie = new BilibiliCookieLoginService().CreateCookie(
        [
            new("SESSDATA", "session", "/", ".bilibili.com"),
            new("bili_jct", "csrf", "/", "passport.bilibili.com"),
            new("DedeUserID", "42", "/", ".bilibili.com"),
            new("buvid3", "device3", "/", ".bilibili.com"),
            new("buvid4", "device4", "/", ".bilibili.com"),
            new("buvid_fp", "fingerprint", "/", "live.bilibili.com"),
            new("SESSDATA", "foreign-session", "/", ".qq.com"),
            new("bili_jct", "foreign-csrf", "/", "bilibili.com.example.com")
        ]);

        Assert.Equal("buvid3=device3;SESSDATA=session;bili_jct=csrf;DedeUserId=42;buvid4=device4;buvid_fp=fingerprint",
            cookie.ToCookieString());
    }

    /// <summary>第三方凭据不能构成 B 站登录，缺字段错误不依赖本地化初始化。</summary>
    [Fact]
    public void MissingCredentialsKeepTheirExceptionTypeAndStructuredError()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new BilibiliCookieLoginService().CreateCookie(
        [
            new Cookie("SESSDATA", "foreign-session", "/", ".example.com"),
            new Cookie("bili_jct", "foreign-csrf", "/", ".example.com")
        ]));

        Assert.True(CoreExceptionInfo.TryGet(error, out var info));
        Assert.Equal(CoreErrorCode.BrowserLoginBilibiliCookiesMissing, info.Code);
        Assert.DoesNotContain("foreign-session", error.ToString());
    }
}
