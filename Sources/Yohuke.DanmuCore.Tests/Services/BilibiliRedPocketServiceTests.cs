using Xunit;
using Yohuke.DanmuCore.Apis;
using Yohuke.DanmuCore.Errors;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Services;

/// <summary>验证登录态红包补查的映射、身份约束和错误边界。</summary>
public sealed class BilibiliRedPocketServiceTests
{
    /// <summary>当前红包列表保留详情，并将账号凭据送到对应房间端点。</summary>
    [Fact]
    public async Task ActiveListPreservesDetailsAndUsesAccountCookie()
    {
        using var handler = new ModerationHttpHandler();
        handler.Responses.Enqueue("""
            {"code":0,"data":{"list":[{"lot_id":7,"sender_uid":42,"sender_name":"用户",
            "start_time":1800000000,"total_price":1000,"danmu":"参与红包",
            "awards":{"1":{"gift_id":"1","gift_name":"礼物","num":2}}}]}}
            """);
        using var client = new HttpClient(handler);

        var messages = await new BilibiliRedPocketService(client).GetActiveAsync("100", "SESSDATA=session;bili_jct=csrf");

        var message = Assert.Single(messages);
        Assert.Equal("100", message.RoomId);
        Assert.Equal(7, message.RedPocket.LotId);
        Assert.Equal(42, message.RedPocket.SenderUid);
        Assert.Equal("参与红包", message.RedPocket.Danmu);
        Assert.Equal("礼物", Assert.Single(message.RedPocket.Awards).Name);
        Assert.True(message.RedPocket.HasDetails);
        Assert.Contains("room_id=100", Assert.Single(handler.Requests).Query);
        Assert.EndsWith("/RedPocketActiveList", handler.Requests[0].AbsolutePath);
        Assert.Contains("SESSDATA=session", handler.Cookie);
    }

    /// <summary>详情只返回请求红包，未出现该红包时表示暂未可用。</summary>
    [Fact]
    public async Task InfoReturnsOnlyTheRequestedLot()
    {
        using var handler = new ModerationHttpHandler();
        handler.Responses.Enqueue("""{"code":0,"data":{"lot_info":[{"lot_id":8},{"lot_id":7,"sender_uid":42}]}}""");
        handler.Responses.Enqueue("""{"code":0,"data":{"lot_info":[{"lot_id":8}]}}""");
        using var client = new HttpClient(handler);
        var service = new BilibiliRedPocketService(client);

        var message = await service.GetInfoAsync("100", 7, "");
        var missing = await service.GetInfoAsync("100", 7, "");

        Assert.Equal(7, message!.RedPocket.LotId);
        Assert.Null(missing);
        Assert.All(handler.Requests, uri => Assert.Contains("lot_id=7", uri.Query));
    }

    /// <summary>结果保留中奖者；尚未就绪与错误红包身份有不同结果。</summary>
    [Fact]
    public async Task WinnersDistinguishReadyMissingAndMismatchedResults()
    {
        using var handler = new ModerationHttpHandler();
        handler.Responses.Enqueue("""
            {"code":0,"data":{"lot_id":7,"lot_info":{"lot_id":7,"sender_uid":42},
            "winner_info":[{"uid":99,"uname":"中奖者","gift_id":"1","gift_num":2}]}}
            """);
        handler.Responses.Enqueue("""{"code":0,"data":{"lot_id":7,"winner_info":[]}}""");
        handler.Responses.Enqueue("""{"code":0,"data":{"lot_id":8,"winner_info":[]}}""");
        using var client = new HttpClient(handler);
        var service = new BilibiliRedPocketService(client);

        var ready = await service.GetWinnersAsync("100", 7, "");
        var missing = await service.GetWinnersAsync("100", 7, "");

        Assert.True(ready!.RedPocket.HasResult);
        Assert.Equal(99, Assert.Single(ready.RedPocket.Winners).Uid);
        Assert.Null(missing);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.GetWinnersAsync("100", 7, ""));
    }

    /// <summary>明确失效保留认证异常，普通失败保留操作异常，错误参数不泄露凭据。</summary>
    [Theory]
    [InlineData(-101)]
    [InlineData(-412)]
    public async Task BusinessFailurePreservesTypeAndSanitizedError(int apiCode)
    {
        using var handler = new ModerationHttpHandler();
        handler.Responses.Enqueue($$"""{"code":{{apiCode}},"message":"failure private-session"}""");
        using var client = new HttpClient(handler);
        var service = new BilibiliRedPocketService(client);

        var error = await Record.ExceptionAsync(() => service.GetActiveAsync("100", "SESSDATA=private-session;bili_jct=private-csrf"));

        Assert.NotNull(error);
        Assert.Equal(apiCode == -101 ? typeof(BilibiliAuthenticationException) : typeof(InvalidOperationException), error.GetType());
        Assert.True(CoreExceptionInfo.TryGet(error, out var info));
        Assert.Equal(CoreErrorCode.RedPocketRequestFailed, info.Code);
        Assert.Equal(apiCode, info.Arguments[0]);
        Assert.DoesNotContain("private-session", info.DiagnosticMessage);
        Assert.DoesNotContain("private-session", error.ToString());
    }
}
