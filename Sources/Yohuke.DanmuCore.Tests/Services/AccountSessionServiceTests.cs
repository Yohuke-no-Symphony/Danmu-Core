using Xunit;
using Yohuke.DanmuCore.Models.Accounts;
using Yohuke.DanmuCore.Services;

namespace Yohuke.DanmuCore.Tests.Services;

/// <summary>验证显式凭据更新、账号删除与迟到校验的隔离。</summary>
public sealed class AccountSessionServiceTests
{
    /// <summary>新凭据生效后，旧验证结果不会覆盖新身份。</summary>
    [Fact]
    public async Task LateResultCannotInvalidateNewCookie()
    {
        var validation = new ControlledAccountValidationService();
        using var sessions = new BilibiliAccountSessionService(validation);
        const string ACCOUNT_ID = "account";
        const string OLD_COOKIE = "SESSDATA=first;bili_jct=csrf";
        const string NEW_COOKIE = "SESSDATA=second;bili_jct=csrf";
        sessions.Register(ACCOUNT_ID, OLD_COOKIE);
        var oldToken = sessions.GetCredentialCancellationToken(ACCOUNT_ID, OLD_COOKIE);
        var first = sessions.ValidateAsync(ACCOUNT_ID);

        sessions.UpdateCredentials(ACCOUNT_ID, NEW_COOKIE);
        var second = sessions.ValidateAsync(ACCOUNT_ID);
        validation.Requests[1].SetResult(new() { Status = AccountValidationStatus.Valid, Uid = 42, UserName = "有效" });
        await second;
        validation.Requests[0].SetResult(new() { Status = AccountValidationStatus.Expired });
        await first;

        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(sessions.CanSend(ACCOUNT_ID, OLD_COOKIE));
        Assert.True(sessions.CanSend(ACCOUNT_ID, NEW_COOKIE));
        Assert.Equal(AccountValidationStatus.Valid, sessions.GetState(ACCOUNT_ID).Status);
    }

    /// <summary>暂时不可验证不会解除已经确认的凭据失效门禁。</summary>
    [Fact]
    public async Task UnavailableDoesNotUnblockKnownExpiredCredentials()
    {
        var validation = new ControlledAccountValidationService();
        using var sessions = new BilibiliAccountSessionService(validation);
        const string COOKIE = "SESSDATA=offline;bili_jct=csrf";
        sessions.Register("account", COOKIE);
        sessions.ReportAuthenticationFailure("account", COOKIE);
        var check = sessions.ValidateAsync("account");
        validation.Requests[0].SetResult(new() { Status = AccountValidationStatus.Unavailable });
        await check;

        Assert.Equal(AccountValidationStatus.Unavailable, sessions.GetState("account").Status);
        Assert.False(sessions.CanSend("account", COOKIE));
    }

    /// <summary>删除账号会撤销旧令牌，迟到结果不能重建账号。</summary>
    [Fact]
    public async Task RemovedAccountCannotBeRevivedByLateValidation()
    {
        var validation = new ControlledAccountValidationService();
        using var sessions = new BilibiliAccountSessionService(validation);
        const string COOKIE = "SESSDATA=offline;bili_jct=csrf";
        sessions.Register("account", COOKIE);
        var token = sessions.GetCredentialCancellationToken("account", COOKIE);
        var check = sessions.ValidateAsync("account");
        sessions.Remove("account");
        validation.Requests[0].SetResult(new() { Status = AccountValidationStatus.Valid, Uid = 42, UserName = "用户" });
        await check;

        Assert.True(token.IsCancellationRequested);
        Assert.False(sessions.CanSend("account", COOKIE));
        Assert.Equal(AccountValidationStatus.NotBound, sessions.GetState("account").Status);
    }

    /// <summary>业务接口确认失效后，已在途的成功验证不得重新解锁凭据。</summary>
    [Fact]
    public async Task AuthenticationFailureWinsOverPendingValidation()
    {
        var validation = new ControlledAccountValidationService();
        using var sessions = new BilibiliAccountSessionService(validation);
        const string COOKIE = "SESSDATA=offline;bili_jct=csrf";
        sessions.Register("account", COOKIE);
        var check = sessions.ValidateAsync("account");

        sessions.ReportAuthenticationFailure("account", COOKIE);
        validation.Requests[0].SetResult(new() { Status = AccountValidationStatus.Valid, Uid = 42 });
        await check;

        Assert.Equal(AccountValidationStatus.Expired, sessions.GetState("account").Status);
        Assert.False(sessions.CanSend("account", COOKIE));
    }

    /// <summary>相同凭据更新不会重置门禁或撤销有效令牌。</summary>
    [Fact]
    public void UnchangedCredentialsKeepTheKnownState()
    {
        using var sessions = new BilibiliAccountSessionService();
        const string COOKIE = "SESSDATA=offline;bili_jct=csrf";
        sessions.Register("account", COOKIE);
        sessions.ReportAuthenticationFailure("account", COOKIE);
        var token = sessions.GetCredentialCancellationToken("account", COOKIE);

        sessions.UpdateCredentials("account", COOKIE);

        Assert.False(token.IsCancellationRequested);
        Assert.False(sessions.CanSend("account", COOKIE));
        Assert.Equal(AccountValidationStatus.Expired, sessions.GetState("account").Status);
    }

    /// <summary>清空和替换凭据只重置对应账号，释放服务撤销全部令牌。</summary>
    [Fact]
    public void CredentialChangesAndDisposalRespectAccountBoundaries()
    {
        var sessions = new BilibiliAccountSessionService();
        const string COOKIE = "SESSDATA=offline;bili_jct=csrf";
        sessions.Register("first", COOKIE);
        sessions.Register("second", COOKIE);
        var firstToken = sessions.GetCredentialCancellationToken("first", COOKIE);
        var secondToken = sessions.GetCredentialCancellationToken("second", COOKIE);

        sessions.UpdateCredentials("first", "");

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
        Assert.Equal(AccountValidationStatus.NotBound, sessions.GetState("first").Status);
        Assert.True(sessions.CanSend("second", COOKIE));

        sessions.Dispose();

        Assert.True(secondToken.IsCancellationRequested);
        Assert.True(sessions.GetCredentialCancellationToken("second", COOKIE).IsCancellationRequested);
        Assert.False(sessions.CanSend("second", COOKIE));
    }
}
