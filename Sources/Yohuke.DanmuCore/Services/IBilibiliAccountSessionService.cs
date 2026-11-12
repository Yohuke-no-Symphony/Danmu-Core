using Yohuke.DanmuCore.Models.Accounts;

namespace Yohuke.DanmuCore.Services;

/// <summary>管理账号凭据版本、验证结果及发送门禁。</summary>
public interface IBilibiliAccountSessionService : IDisposable
{
    /// <summary>指定本地账号的运行时状态发生变化。</summary>
    event EventHandler<string>? Changed;

    /// <summary>注册账号及其当前凭据，不主动联网。</summary>
    void Register(string accountId, string cookie);

    /// <summary>更新凭据并撤销旧凭据关联的操作。</summary>
    void UpdateCredentials(string accountId, string cookie);

    /// <summary>删除运行时账号并撤销旧请求。</summary>
    void Remove(string accountId);

    /// <summary>获取当前账号验证状态。</summary>
    AccountValidationResult GetState(string accountId);

    /// <summary>验证当前凭据，并隔离旧凭据的迟到响应。</summary>
    Task<AccountValidationResult> ValidateAsync(string accountId, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>判断指定账号的当前凭据是否允许继续发送。</summary>
    bool CanSend(string accountId, string cookie);

    /// <summary>获取随凭据变更、删除或服务释放而取消的令牌。</summary>
    CancellationToken GetCredentialCancellationToken(string accountId, string cookie);

    /// <summary>记录当前凭据收到的明确未登录响应。</summary>
    void ReportAuthenticationFailure(string accountId, string cookie);
}
