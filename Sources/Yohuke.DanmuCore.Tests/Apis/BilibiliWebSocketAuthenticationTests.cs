using System.Buffers.Binary;
using System.Text;
using Yohuke.DanmuCore.Apis;
using Yohuke.DanmuCore.Apis.Models.Bilibili;
using Yohuke.DanmuCore.Apis.Models.Common;
using Yohuke.DanmuCore.Errors;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Apis;

/// <summary>验证弹幕配置、连接鉴权与默认重连契约。</summary>
public sealed class BilibiliWebSocketAuthenticationTests
{
    /// <summary>现有构造调用继续默认自动重连。</summary>
    [Fact]
    public void ExistingSocketConstructionKeepsAutomaticRecovery()
    {
        using var socket = new BilibiliLiveWebSocket(100, "");

        Assert.Equal(LiveDanmuReconnectMode.Automatic, socket.ReconnectMode);
    }

    /// <summary>弹幕配置的业务失败先于数据字段判断，只有明确未登录才标记账号失效。</summary>
    [Theory]
    [InlineData(-101, true)]
    [InlineData(-400, false)]
    [InlineData(-403, false)]
    public void ConfigurationFailurePreservesBusinessCodeAndAuthenticationMeaning(int code, bool authenticationFailure)
    {
        var response = new ApiResponse<BilibiliDanmuInfoData> { Code = code, Data = new() };

        var failure = Assert.ThrowsAny<InvalidOperationException>(() => BilibiliLiveWebSocket.RequireDanmuConfiguration(response));

        Assert.Equal(authenticationFailure, failure is BilibiliAuthenticationException);
        Assert.True(CoreExceptionInfo.TryGet(failure, out var info));
        Assert.Equal(CoreErrorCode.WebSocketConfigurationRequestFailed, info.Code);
        Assert.Equal(code, Assert.Single(info.Arguments));
    }

    /// <summary>业务成功但缺少配置不能建立连接。</summary>
    [Fact]
    public void SuccessfulConfigurationRequiresServerData()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => BilibiliLiveWebSocket.RequireDanmuConfiguration(new()));

        Assert.True(CoreExceptionInfo.TryGet(failure, out var info));
        Assert.Equal(CoreErrorCode.WebSocketServerDataMissing, info.Code);
        var data = new BilibiliDanmuInfoData();
        Assert.Same(data, BilibiliLiveWebSocket.RequireDanmuConfiguration(new() { Data = data }));
    }

    /// <summary>仅收到成功鉴权回复时通知连接成功，同帧后续业务消息继续分发。</summary>
    [Fact]
    public void AuthenticationReplyConfirmsConnectionAndPreservesFollowingMessages()
    {
        using var socket = new BilibiliLiveWebSocket(100, "");
        var authenticated = 0;
        var messages = 0;
        socket.RawBusinessEventReceived += (_, _) => messages++;
        socket.AnalysePackage(Packet("", 3), () => authenticated++);
        Assert.Equal(0, authenticated);

        socket.AnalysePackage([.. Packet("{\"code\":0}", 8), .. Packet("{\"cmd\":\"TEST_EVENT\"}", 5)], () => authenticated++);

        Assert.Equal(1, authenticated);
        Assert.Equal(1, messages);
    }

    /// <summary>入房被拒绝直接终止连接，不作为可忽略的单包诊断。</summary>
    [Theory]
    [InlineData(-101, true)]
    [InlineData(-400, false)]
    public void RejectedAuthenticationPropagatesTypedFailure(int code, bool authenticationFailure)
    {
        using var socket = new BilibiliLiveWebSocket(100, "");
        var authenticated = 0;
        var diagnostics = 0;
        socket.ErrorReceived += (_, _) => diagnostics++;

        var failure = Assert.ThrowsAny<InvalidOperationException>(() => socket.AnalysePackage(Packet($"{{\"code\":{code}}}", 8), () => authenticated++));

        Assert.Equal(authenticationFailure, failure is BilibiliAuthenticationException);
        Assert.True(CoreExceptionInfo.TryGet(failure, out var info));
        Assert.Equal(CoreErrorCode.WebSocketAuthenticationFailed, info.Code);
        Assert.Equal(code, Assert.Single(info.Arguments));
        Assert.Equal(0, authenticated);
        Assert.Equal(0, diagnostics);
    }

    /// <summary>缺失或损坏的鉴权结果不能被当成成功入房。</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":\"0\"}")]
    [InlineData("{\"code\":null}")]
    [InlineData("{\"code\":999999999999}")]
    [InlineData("{invalid")]
    public void MalformedAuthenticationReplyStopsConnection(string body)
    {
        using var socket = new BilibiliLiveWebSocket(100, "");

        var failure = Assert.Throws<InvalidOperationException>(() => socket.AnalysePackage(Packet(body, 8)));

        Assert.True(CoreExceptionInfo.TryGet(failure, out var info));
        Assert.Equal(CoreErrorCode.WebSocketAuthenticationResponseInvalid, info.Code);
    }

    /// <summary>没有鉴权回复时有界结束等待，主动取消仍保持取消语义。</summary>
    [Fact]
    public async Task AuthenticationWaitIsBoundedAndCancellable()
    {
        var authenticated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var timeout = await Assert.ThrowsAsync<TimeoutException>(() => BilibiliLiveWebSocket.WaitForAuthenticationAsync(authenticated.Task, TimeSpan.FromMilliseconds(20), CancellationToken.None));

        Assert.True(CoreExceptionInfo.TryGet(timeout, out var info));
        Assert.Equal(CoreErrorCode.WebSocketAuthenticationTimeout, info.Code);
        using var cancellation = new CancellationTokenSource();
        var waiting = BilibiliLiveWebSocket.WaitForAuthenticationAsync(authenticated.Task, TimeSpan.FromSeconds(30), cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        authenticated.TrySetResult();
        await BilibiliLiveWebSocket.WaitForAuthenticationAsync(authenticated.Task, TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    private static byte[] Packet(string body, int operation)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var packet = new byte[16 + bytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(0, 4), packet.Length);
        BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(6, 2), 1);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8, 4), operation);
        bytes.CopyTo(packet, 16);
        return packet;
    }
}
