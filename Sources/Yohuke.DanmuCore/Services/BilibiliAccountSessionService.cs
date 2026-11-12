using Yohuke.DanmuCore.Models.Accounts;

namespace Yohuke.DanmuCore.Services;

/// <summary>
/// 应用生命周期内共享的账号状态与凭据取消边界。
/// </summary>
public sealed class BilibiliAccountSessionService : IBilibiliAccountSessionService
{
    private readonly IBilibiliAccountValidationService validationService;
    private readonly Dictionary<string, AccountSessionEntry> entries = [];
    private readonly object sync = new();
    private bool isDisposed;

    /// <summary>
    /// 初始化账号会话，不主动联网。
    /// </summary>
    public BilibiliAccountSessionService(IBilibiliAccountValidationService? validationService = null)
    {
        this.validationService = validationService ?? new BilibiliAccountValidationService();
    }

    /// <inheritdoc/>
    public event EventHandler<string>? Changed;

    /// <inheritdoc/>
    public void Register(string accountId, string cookie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(cookie);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (entries.ContainsKey(accountId))
            {
                return;
            }

            entries.Add(accountId, new()
            {
                Cookie = cookie,
                State = new()
                {
                    Status = string.IsNullOrWhiteSpace(cookie)
                        ? AccountValidationStatus.NotBound : AccountValidationStatus.Unchecked
                }
            });
        }
    }

    /// <inheritdoc/>
    public void Remove(string accountId)
    {
        lock (sync)
        {
            if (!entries.Remove(accountId, out var entry))
            {
                return;
            }

            entry.Credentials.Cancel();
            entry.Credentials.Dispose();
            entry.Validation?.Cancel();
        }

        Changed?.Invoke(this, accountId);
    }

    /// <inheritdoc/>
    public AccountValidationResult GetState(string accountId)
    {
        lock (sync)
        {
            return entries.TryGetValue(accountId, out var entry)
                ? entry.State : new() { Status = AccountValidationStatus.NotBound };
        }
    }

    /// <inheritdoc/>
    public bool CanSend(string accountId, string cookie)
    {
        lock (sync)
        {
            return !isDisposed && entries.TryGetValue(accountId, out var entry) && entry.Cookie == cookie &&
                   !entry.IsBlocked && BilibiliAccountValidationService.HasSendingCredentials(cookie);
        }
    }

    /// <inheritdoc/>
    public CancellationToken GetCredentialCancellationToken(string accountId, string cookie)
    {
        lock (sync)
        {
            return !isDisposed && entries.TryGetValue(accountId, out var entry) && entry.Cookie == cookie
                ? entry.Credentials.Token : new(true);
        }
    }

    /// <inheritdoc/>
    public async Task<AccountValidationResult> ValidateAsync(string accountId, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        AccountSessionEntry entry;
        CancellationTokenSource request;
        long revision;
        string cookie;

        lock (sync)
        {
            if (isDisposed || !entries.TryGetValue(accountId, out entry!))
            {
                return new() { Status = AccountValidationStatus.NotBound };
            }

            entry.Validation?.Cancel();
            request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, entry.Credentials.Token);
            entry.Validation = request;
            revision = ++entry.Revision;
            cookie = entry.Cookie;
            entry.State = entry.State with { Status = AccountValidationStatus.Checking };
        }

        Changed?.Invoke(this, accountId);

        try
        {
            var result = await validationService.ValidateAsync(cookie, timeout, request.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (isDisposed || !entries.TryGetValue(accountId, out var current) ||
                    !ReferenceEquals(current, entry) || entry.Revision != revision || request.IsCancellationRequested)
                {
                    return GetState(accountId);
                }

                entry.IsBlocked = result.Status switch
                {
                    AccountValidationStatus.Valid => false,
                    AccountValidationStatus.Expired or AccountValidationStatus.Incomplete or AccountValidationStatus.NotBound => true,
                    _ => entry.IsBlocked
                };
                entry.State = result with
                {
                    Uid = result.Uid > 0 ? result.Uid : entry.State.Uid,
                    UserName = result.Uid > 0 ? result.UserName : entry.State.UserName
                };
            }

            Changed?.Invoke(this, accountId);
            return GetState(accountId);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            return GetState(accountId);
        }
        catch
        {
            lock (sync)
            {
                if (!isDisposed && entries.TryGetValue(accountId, out var current) &&
                    ReferenceEquals(current, entry) && entry.Revision == revision)
                {
                    entry.State = entry.State with
                    {
                        Status = AccountValidationStatus.Unavailable, CheckedAt = DateTimeOffset.Now
                    };
                }
            }

            Changed?.Invoke(this, accountId);
            return GetState(accountId);
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(entry.Validation, request))
                {
                    entry.Validation = null;
                }
            }

            request.Dispose();
        }
    }

    /// <inheritdoc/>
    public void ReportAuthenticationFailure(string accountId, string cookie)
    {
        lock (sync)
        {
            if (!entries.TryGetValue(accountId, out var entry) || entry.Cookie != cookie)
            {
                return;
            }

            entry.Revision++;
            entry.Validation?.Cancel();
            entry.IsBlocked = true;
            entry.State = entry.State with { Status = AccountValidationStatus.Expired, CheckedAt = DateTimeOffset.Now };
        }

        Changed?.Invoke(this, accountId);
    }

    /// <inheritdoc/>
    public void UpdateCredentials(string accountId, string cookie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(cookie);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!entries.TryGetValue(accountId, out var entry) || entry.Cookie == cookie)
            {
                return;
            }

            entry.Revision++;
            entry.Validation?.Cancel();
            entry.Credentials.Cancel();
            entry.Credentials.Dispose();
            entry.Credentials = new();
            entry.Cookie = cookie;
            entry.IsBlocked = false;
            entry.State = new()
            {
                Status = string.IsNullOrWhiteSpace(cookie)
                    ? AccountValidationStatus.NotBound : AccountValidationStatus.Unchecked
            };
        }

        Changed?.Invoke(this, accountId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (sync)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            foreach (var accountId in entries.Keys.ToArray())
            {
                Remove(accountId);
            }
        }
    }
}
