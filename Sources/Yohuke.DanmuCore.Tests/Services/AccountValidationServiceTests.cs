using Xunit;
using Yohuke.DanmuCore.Models.Accounts;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Services;

/// <summary>
/// 验证真实登录结果、凭据完整性与暂时无法验证的边界。
/// </summary>
public class AccountValidationServiceTests
{
    /// <summary>
    /// 仅明确的登录结果决定有效性，缺字段和风控不等于过期。
    /// </summary>
    [Theory]
    [InlineData("{\"code\":0,\"data\":{\"isLogin\":true,\"mid\":42,\"uname\":\"测试用户\"}}", AccountValidationStatus.Valid)]
    [InlineData("{\"code\":-101,\"data\":{\"isLogin\":false}}", AccountValidationStatus.Expired)]
    [InlineData("{\"code\":0,\"data\":{\"isLogin\":false}}", AccountValidationStatus.Expired)]
    [InlineData("{\"code\":-412,\"message\":\"请稍后重试\"}", AccountValidationStatus.Unavailable)]
    [InlineData("{\"code\":0,\"data\":{\"mid\":42}}", AccountValidationStatus.Unavailable)]
    [InlineData("not json", AccountValidationStatus.Unavailable)]
    [InlineData("{\"code\":0,\"data\":{\"isLogin\":true,\"mid\":43,\"uname\":\"另一用户\"}}", AccountValidationStatus.Incomplete)]
    public async Task NavigationResponseClassifiesWithoutGuessing(string json, AccountValidationStatus expected)
    {
        using var handler = new ModerationHttpHandler();
        handler.Responses.Enqueue(json);
        using var client = new HttpClient(handler);
        var service = new BilibiliAccountValidationService(client);

        var result = await service.ValidateAsync("SESSDATA=offline;bili_jct=csrf;DedeUserID=42");

        Assert.Equal(expected, result.Status);
        Assert.Equal("/x/web-interface/nav", handler.Requests.Single().AbsolutePath);
        Assert.Contains("SESSDATA=offline", handler.Cookie);
        if (expected == AccountValidationStatus.Valid)
        {
            Assert.Equal(42, result.Uid);
            Assert.Equal("测试用户", result.UserName);
        }
    }

    /// <summary>
    /// 不完整的发送凭据在本地识别，不向外发请求。
    /// </summary>
    [Theory]
    [InlineData("", AccountValidationStatus.NotBound)]
    [InlineData("SESSDATA=offline", AccountValidationStatus.Incomplete)]
    public async Task MissingCredentialsDoNotRequestNavigation(string cookie, AccountValidationStatus expected)
    {
        using var handler = new ModerationHttpHandler();
        using var client = new HttpClient(handler);

        var result = await new BilibiliAccountValidationService(client).ValidateAsync(cookie);

        Assert.Equal(expected, result.Status);
        Assert.Empty(handler.Requests);
    }

    /// <summary>调用方取消不会被误判为暂时无法验证。</summary>
    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RoomDiscoveryHttpHandler
        {
            RespondAsync = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return "";
            }
        };
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var request = new BilibiliAccountValidationService(client).ValidateAsync("SESSDATA=offline;bili_jct=csrf",
            cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }
}
