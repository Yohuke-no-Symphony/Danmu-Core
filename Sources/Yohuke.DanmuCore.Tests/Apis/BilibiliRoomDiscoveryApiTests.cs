using System.Net;
using Yohuke.DanmuCore.Apis;
using Yohuke.DanmuCore.Errors;
using Yohuke.DanmuCore.Models.Search;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Apis;

/// <summary>验证房间发现使用的公开 UID 关联和搜索设备初始化协议。</summary>
public sealed class BilibiliRoomDiscoveryApiTests
{
    private const string DEVICE_PATH = "/x/frontend/finger/spi";
    private const string SEARCH_PATH = "/x/web-interface/search/type";
    private const string DEVICE_JSON = """{"code":0,"data":{"b_3":"anonymous-device","b_4":"anonymous-v4"}}""";
    private const string NAV_JSON = """{"code":-101,"data":{"wbi_img":{"img_url":"https://i0.hdslb.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://i0.hdslb.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""";

    /// <summary>UID 通过直播平台查询关联房号，资料请求保持匿名。</summary>
    [Fact]
    public async Task UidSearchUsesLiveRoomAssociationWithoutProfileQuery()
    {
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(Json(request.RequestUri!.AbsolutePath switch
            {
                "/room/v1/Room/getRoomInfoOld" => """{"code":0,"data":{"roomStatus":1,"roomid":21573665,"liveStatus":1}}""",
                "/x/web-interface/nav" => NAV_JSON,
                "/xlive/web-room/v1/index/getInfoByRoom" when request.RequestUri.Query.Contains("room_id=21573665") => """{"code":0,"data":{"room_info":{"room_id":21573665,"uid":442902274},"anchor_info":{"base_info":{"uname":"主播"}}}}""",
                "/xlive/web-room/v1/index/getInfoByRoom" => """{"code":19002000,"message":"房间不存在"}""",
                _ => throw new InvalidOperationException("Unexpected profile lookup")
            }));
        });
        using var http = new HttpClient(handler);

        var page = await new BilibiliLiveRoomSearchService(http).SearchAsync("442902274", cookie: "SESSDATA=private-session");

        var room = Assert.Single(page.Results);
        Assert.Equal("21573665", room.RoomId);
        Assert.Equal(LiveRoomSearchSource.UserId, room.Sources);
        Assert.DoesNotContain(page.Issues, issue => issue.Source == LiveRoomSearchSource.UserId);
    }

    /// <summary>缺少设备字段时匿名取设备标识，搜索保留显式身份且不混入客户端默认账号。</summary>
    [Theory]
    [InlineData("", "", "anonymous-device", 2)]
    [InlineData("SESSDATA=explicit", "explicit", "anonymous-device", 2)]
    [InlineData("SESSDATA=explicit;buvid3=existing", "explicit", "existing", 1)]
    public async Task LiveSearchInitializesMissingDeviceAndKeepsExplicitIdentity(string cookie, string expectedSession, string expectedDevice, int expectedRequests)
    {
        var requests = 0;
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath == DEVICE_PATH)
            {
                Assert.False(request.Headers.Contains("Cookie"));
                return Task.FromResult(Json(DEVICE_JSON));
            }
            Assert.Equal(SEARCH_PATH, request.RequestUri.AbsolutePath);
            Assert.Equal("https://search.bilibili.com/", request.Headers.Referrer!.AbsoluteUri);
            var actualCookie = request.Headers.GetValues("Cookie").Single();
            Assert.Equal(expectedSession, ApiCookieHelper.GetValue(actualCookie, "SESSDATA"));
            Assert.Equal(expectedDevice, ApiCookieHelper.GetValue(actualCookie, "buvid3"));
            Assert.DoesNotContain("client-session", actualCookie);
            return Task.FromResult(Json("""{"code":0,"data":{"result":[]}}"""));
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=client-session", httpClient: http);

        await api.SearchLiveUsersAsync("主播", cookie: cookie);

        Assert.Equal(expectedRequests, requests);
    }

    /// <summary>设备初始化和搜索共享调用方给定的超时。</summary>
    [Theory]
    [InlineData(DEVICE_PATH)]
    [InlineData(SEARCH_PATH)]
    public async Task LiveSearchTimeoutCoversDeviceInitializationAndSearch(string delayedPath)
    {
        using var handler = new ApiTestHttpHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == delayedPath)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return Json(request.RequestUri.AbsolutePath == DEVICE_PATH ? DEVICE_JSON : """{"code":0,"data":{"result":[]}}""");
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi(httpClient: http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.SearchLiveUsersAsync("主播",
            timeout: TimeSpan.FromMilliseconds(100)).WaitAsync(TimeSpan.FromSeconds(2)));
    }

    /// <summary>设备初始化失败保留本地错误语义，不继续发送缺少必要设备信息的搜索。</summary>
    [Fact]
    public async Task MissingDevicePayloadFailsBeforeSearch()
    {
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            Assert.Equal(DEVICE_PATH, request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json("""{"code":0,"data":{}}"""));
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi(httpClient: http);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => api.SearchLiveUsersAsync("主播"));

        Assert.True(CoreExceptionInfo.TryGet(error, out var info));
        Assert.Equal(CoreErrorCode.RoomSearchFailed, info.Code);
    }

    /// <summary>未开通直播间是有效空结果，缺少关联字段的响应必须保留为查询问题。</summary>
    [Theory]
    [InlineData("""{"code":0,"data":{"roomStatus":0,"roomid":0}}""", false)]
    [InlineData("""{"code":0,"data":{}}""", true)]
    public async Task UidLookupDistinguishesNoLiveRoomFromMissingPayload(string association, bool hasIssue)
    {
        using var handler = new ApiTestHttpHandler((request, _) => Task.FromResult(Json(request.RequestUri!.AbsolutePath switch
        {
            "/room/v1/Room/getRoomInfoOld" => association,
            "/x/web-interface/nav" => NAV_JSON,
            _ => """{"code":0,"data":{"room_info":{"room_id":42,"uid":123},"anchor_info":{"base_info":{"uname":"主播"}}}}"""
        })));
        using var http = new HttpClient(handler);

        var page = await new BilibiliLiveRoomSearchService(http).SearchAsync("42");

        Assert.Equal(LiveRoomSearchSource.RoomId, Assert.Single(page.Results).Sources);
        Assert.Equal(hasIssue, page.Issues.Any(issue => issue.Source == LiveRoomSearchSource.UserId));
        if (hasIssue)
        {
            Assert.Equal(CoreErrorCode.RoomSearchResultsMissing, Assert.Single(page.Issues).Error!.Code);
        }
    }

    private static HttpResponseMessage Json(string content)
    {
        return new(HttpStatusCode.OK) { Content = new StringContent(content) };
    }
}
