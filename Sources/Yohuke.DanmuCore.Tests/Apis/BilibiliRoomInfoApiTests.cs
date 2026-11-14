using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Yohuke.DanmuCore.Apis;

namespace Yohuke.DanmuCore.Tests.Apis;

/// <summary>验证直播间资料的签名协议、公开身份边界和请求生命周期。</summary>
public sealed class BilibiliRoomInfoApiTests
{
    private const string NAV_PATH = "/x/web-interface/nav";
    private const string ROOM_PATH = "/xlive/web-room/v1/index/getInfoByRoom";
    private const string NAV_JSON = """{"code":-101,"data":{"wbi_img":{"img_url":"https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png"}}}""";

    /// <summary>资料请求使用目标房间和来源参数签名，公开查询不发送客户端登录凭据。</summary>
    [Fact]
    public async Task RoomInfoSignsRoomAndWebLocationWithoutSendingAccountCredentials()
    {
        var requests = 0;
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            requests++;
            Assert.False(request.Headers.Contains("Cookie"));
            if (request.RequestUri!.AbsolutePath == NAV_PATH)
            {
                return Task.FromResult(Json(NAV_JSON));
            }

            Assert.Equal(ROOM_PATH, request.RequestUri.AbsolutePath);
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            Assert.Equal("21573665", query["room_id"]);
            Assert.Equal("444.8", query["web_location"]);
            Assert.True(long.TryParse(query["wts"], out var timestamp));
            Assert.InRange(timestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60);
            var source = $"room_id=21573665&web_location=444.8&wts={query["wts"]}ea1db124af3c7062474693fa704f4ff8";
            var expectedSignature = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
            Assert.Equal(expectedSignature, query["w_rid"]);
            Assert.Equal("https://live.bilibili.com", request.Headers.GetValues("Origin").Single());
            return Task.FromResult(Json("""{"code":0,"data":{"room_info":{"room_id":21573665,"uid":42,"title":"直播标题"},"anchor_info":{"base_info":{"uname":"主播"}}}}"""));
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=private-session", httpClient: http);

        var result = await api.GetRoomInfoAsync(21573665);

        Assert.Equal(0, result.Code);
        Assert.Equal(21573665, result.Data!.RoomInfo!.RoomId);
        Assert.Equal("主播", result.Data.AnchorInfo!.BaseInfo!.Name);
        Assert.Equal(2, requests);
    }

    /// <summary>显式身份仅用于房间请求，轮换身份不影响签名取键和原有公开查询。</summary>
    [Fact]
    public async Task ExplicitRoomCredentialsAreNormalizedWithoutChangingAnonymousQueries()
    {
        var roomCookies = new List<string?>();
        var navigationRequests = 0;
        using var handler = new ApiTestHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == NAV_PATH)
            {
                navigationRequests++;
                Assert.False(request.Headers.Contains("Cookie"));
                return Task.FromResult(Json(NAV_JSON));
            }
            Assert.Equal(ROOM_PATH, request.RequestUri.AbsolutePath);
            roomCookies.Add(request.Headers.TryGetValues("Cookie", out var values) ? Assert.Single(values) : null);
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            Assert.Equal("21573665", query["room_id"]);
            Assert.NotEmpty(query["w_rid"]!);
            return Task.FromResult(Json("""{"code":0,"data":{"room_info":{"room_id":21573665}}}"""));
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi("SESSDATA=client-session", httpClient: http);

        var selected = await api.GetRoomInfoWithCredentialsAsync(21573665,
            " DedeUserID = 42 ; SESSDATA = selected-session ; buvid3 = selected-device ; bili_jct = selected-csrf ; ignored = omitted ");
        await api.GetRoomInfoWithCredentialsAsync(21573665, "SESSDATA=replacement-session;DedeUserID=43");
        await api.GetRoomInfoAsync(21573665);

        Assert.Equal(21573665, selected.Data!.RoomInfo!.RoomId);
        Assert.Equal(1, navigationRequests);
        Assert.Equal(new string?[]
        {
            "buvid3=selected-device;SESSDATA=selected-session;bili_jct=selected-csrf;DedeUserId=42",
            "buvid3=;SESSDATA=replacement-session;bili_jct=;DedeUserId=43",
            null
        }, roomCookies);
    }

    /// <summary>调用方设置的超时同时约束签名取键和资料请求。</summary>
    [Theory]
    [InlineData(NAV_PATH, false)]
    [InlineData(ROOM_PATH, false)]
    [InlineData(NAV_PATH, true)]
    [InlineData(ROOM_PATH, true)]
    public async Task RoomInfoTimeoutCoversKeyAcquisitionAndRoomRequest(string delayedPath, bool explicitCredentials)
    {
        using var handler = new ApiTestHttpHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == delayedPath)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            return Json(request.RequestUri.AbsolutePath == NAV_PATH ? NAV_JSON : """{"code":0,"data":{}}""");
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi(httpClient: http);

        var request = explicitCredentials
            ? api.GetRoomInfoWithCredentialsAsync(21573665, "SESSDATA=selected-session", timeout: TimeSpan.FromMilliseconds(100))
            : api.GetRoomInfoAsync(21573665, timeout: TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    /// <summary>取消资料查询时终止当前取键或资料请求，不返回伪造的成功资料。</summary>
    [Theory]
    [InlineData(NAV_PATH, false)]
    [InlineData(ROOM_PATH, false)]
    [InlineData(NAV_PATH, true)]
    [InlineData(ROOM_PATH, true)]
    public async Task RoomInfoCancellationStopsKeyAcquisitionAndRoomRequest(string cancelledPath, bool explicitCredentials)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ApiTestHttpHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == cancelledPath)
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            return Json(request.RequestUri.AbsolutePath == NAV_PATH ? NAV_JSON : """{"code":0,"data":{}}""");
        });
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi(httpClient: http);

        var request = explicitCredentials
            ? api.GetRoomInfoWithCredentialsAsync(21573665, "SESSDATA=selected-session", cancellationToken: cancellation.Token)
            : api.GetRoomInfoAsync(21573665, cancellationToken: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    /// <summary>补齐签名后仍保留上游的业务拒绝，交由调用方处理。</summary>
    [Fact]
    public async Task RoomInfoPreservesUpstreamBusinessError()
    {
        using var handler = new ApiTestHttpHandler((request, _) => Task.FromResult(Json(
            request.RequestUri!.AbsolutePath == NAV_PATH ? NAV_JSON : """{"code":-352,"message":"风控校验失败"}""")));
        using var http = new HttpClient(handler);
        using var api = new BilibiliApi(httpClient: http);

        var result = await api.GetRoomInfoAsync(21573665);

        Assert.Equal(-352, result.Code);
        Assert.Equal("风控校验失败", result.Message);
    }

    private static HttpResponseMessage Json(string content)
    {
        return new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
