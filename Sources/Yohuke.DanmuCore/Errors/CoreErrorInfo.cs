using System.Globalization;
using Yohuke.DanmuCore.Diagnostics;

namespace Yohuke.DanmuCore.Errors;

/// <summary>保存错误语义和不可变标量参数快照，不捕获本地化服务或凭据对象。</summary>
public sealed record CoreErrorInfo
{
    /// <summary>创建与显示语言无关的错误描述。</summary>
    public CoreErrorInfo(CoreErrorCode code, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Code = code;
        Arguments = Array.AsReadOnly(arguments.Select(Snapshot).ToArray());
    }

    /// <summary>错误语义。</summary>
    public CoreErrorCode Code { get; }

    /// <summary>已脱敏的不可变标量参数快照。</summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>不依赖宿主语言资源的固定英文诊断。</summary>
    public string DiagnosticMessage
    {
        get
        {
            var text = Code switch
            {
                CoreErrorCode.ApiEmptyJsonResult => "The JSON response contains no result.",
                CoreErrorCode.WbiImageKeysMissing => "The WBI image keys are missing.",
                CoreErrorCode.WebSocketServerDataMissing => "The danmu server data is missing.",
                CoreErrorCode.WebSocketServerMissing => "No danmu server is available.",
                CoreErrorCode.WebSocketConnectionClosed => "The danmu connection was closed.",
                CoreErrorCode.WebSocketHeartbeatStopped => "The danmu heartbeat sender stopped.",
                CoreErrorCode.WebSocketServerClosed => "The danmu server closed the connection.",
                CoreErrorCode.WebSocketReceivingStopped => "The danmu receiver stopped.",
                CoreErrorCode.WebSocketHeartbeatTimeout => "The danmu server heartbeat timed out.",
                CoreErrorCode.WebSocketInvalidMessage => "The danmu protocol message is invalid.",
                CoreErrorCode.SocketConnectionClosed => "The connection was closed.",
                CoreErrorCode.HistoryInvalidRoomId => "The history room identifier is invalid.",
                CoreErrorCode.HistoryRequestFailed => "The danmu history request failed.",
                CoreErrorCode.RedPocketInvalidRoomId => "The red pocket room identifier is invalid.",
                CoreErrorCode.RedPocketRequestFailed => "The red pocket request failed.",
                CoreErrorCode.RoomSearchFollowingLoginRequired => "Login is required to discover followed live rooms.",
                CoreErrorCode.RoomSearchInvalidIdentifier => "The room search identifier is invalid.",
                CoreErrorCode.RoomSearchResultsMissing => "The room search response is incomplete.",
                CoreErrorCode.RoomSearchFailed => "The room search request failed.",
                CoreErrorCode.BrowserLoginBilibiliCookiesMissing => "The Bilibili login cookies are incomplete.",
                CoreErrorCode.WebSocketConfigurationRequestFailed => "The danmu connection configuration request failed.",
                CoreErrorCode.WebSocketAuthenticationFailed => "The danmu connection authentication was rejected.",
                CoreErrorCode.WebSocketAuthenticationResponseInvalid => "The danmu connection authentication response is invalid.",
                CoreErrorCode.WebSocketAuthenticationTimeout => "The danmu connection authentication timed out.",
                _ => "A shared danmu operation failed."
            };
            return Arguments.Count == 0
                ? text
                : $"{text} ({string.Join(", ", Arguments.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)))})";
        }
    }

    private static object? Snapshot(object? argument)
    {
        return argument switch
        {
            null => null,
            string text => DiagnosticSanitizer.Sanitize(text),
            bool or char or byte or sbyte or short or ushort or int or uint or long or ulong or
                float or double or decimal or Enum or Guid or DateTime or DateTimeOffset or TimeSpan => argument,
            _ => throw new ArgumentException("Core error arguments must be immutable scalar values.", nameof(argument))
        };
    }
}
