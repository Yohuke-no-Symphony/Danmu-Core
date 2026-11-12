using Yohuke.DanmuCore.Models.Accounts;

namespace Yohuke.DanmuCore.Services;

internal sealed class AccountSessionEntry
{
    internal required string Cookie { get; set; }
    internal AccountValidationResult State { get; set; } = new() { Status = AccountValidationStatus.Unchecked };
    internal CancellationTokenSource Credentials { get; set; } = new();
    internal CancellationTokenSource? Validation { get; set; }
    internal long Revision { get; set; }
    internal bool IsBlocked { get; set; }
}
