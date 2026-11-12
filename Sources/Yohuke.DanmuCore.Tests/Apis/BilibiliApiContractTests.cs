using System.Net;
using Yohuke.DanmuCore.Apis;

namespace Yohuke.DanmuCore.Tests.Apis;

/// <summary>验证合并后的用户 API 分页、请求来源与显式身份边界。</summary>
public sealed class BilibiliApiContractTests
{
    /// <summary>昵称搜索保留页码和页大小，默认使用客户端账号。</summary>
    [Fact]
    public async Task UserSearchUsesAccountCookieAndExplicitPagination()
    {
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            Assert.Contains("search_type=bili_user", request.RequestUri!.Query);
            Assert.Contains("page=3", request.RequestUri.Query);
            Assert.Contains("page_size=7", request.RequestUri.Query);
            Assert.Equal("https://search.bilibili.com", request.Headers.GetValues("Origin").Single());
            Assert.Equal("https://search.bilibili.com/", request.Headers.Referrer!.AbsoluteUri);
            Assert.Contains("SESSDATA=account", request.Headers.GetValues("Cookie").Single());
            return Task.FromResult(Response());
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=account", httpClient: http);

        await api.SearchUsersAsync("用户", page: 3, pageSize: 7);
    }

    /// <summary>显式 Cookie 覆盖客户端账号，空字符串允许匿名查询。</summary>
    [Theory]
    [InlineData("SESSDATA=override", "override")]
    [InlineData("", "")]
    public async Task UserSearchDoesNotLeakAccountIntoCookieOverride(string cookie, string expectedSession)
    {
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            var actual = request.Headers.GetValues("Cookie").Single();
            Assert.DoesNotContain("account", actual);
            Assert.Equal(expectedSession, ApiCookieHelper.GetValue(actual, "SESSDATA"));
            return Task.FromResult(Response());
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=account", httpClient: http);

        await api.SearchUsersAsync("用户", cookie: cookie);
    }

    /// <summary>公开名片默认匿名，显式身份仅用于当前请求。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("SESSDATA=explicit")]
    public async Task PublicCardKeepsItsExplicitCookieBoundary(string? cookie)
    {
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            Assert.Equal("/x/web-interface/card", request.RequestUri!.AbsolutePath);
            Assert.Contains("mid=42", request.RequestUri.Query);
            Assert.Equal("https://space.bilibili.com/42", request.Headers.Referrer!.AbsoluteUri);
            if (cookie is null)
            {
                Assert.False(request.Headers.Contains("Cookie"));
            }
            else
            {
                Assert.Contains("SESSDATA=explicit", request.Headers.GetValues("Cookie").Single());
                Assert.DoesNotContain("account", request.Headers.GetValues("Cookie").Single());
            }

            return Task.FromResult(Response());
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=account", httpClient: http);

        await api.GetUserCardContainerAsync(42, cookie: cookie);
    }

    private static HttpResponseMessage Response()
    {
        return new(HttpStatusCode.OK) { Content = new StringContent("{\"code\":0,\"data\":{}}") };
    }
}
