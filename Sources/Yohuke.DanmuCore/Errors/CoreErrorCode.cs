namespace Yohuke.DanmuCore.Errors;

/// <summary>与产品显示语言无关的共享能力错误语义。</summary>
public enum CoreErrorCode
{
    /// <summary>JSON 响应没有可用数据。</summary>
    ApiEmptyJsonResult,
    /// <summary>WBI 签名图片密钥缺失。</summary>
    WbiImageKeysMissing,
    /// <summary>弹幕服务器数据缺失。</summary>
    WebSocketServerDataMissing,
    /// <summary>没有可用弹幕服务器。</summary>
    WebSocketServerMissing,
    /// <summary>弹幕连接关闭。</summary>
    WebSocketConnectionClosed,
    /// <summary>弹幕心跳发送停止。</summary>
    WebSocketHeartbeatStopped,
    /// <summary>服务器关闭弹幕连接。</summary>
    WebSocketServerClosed,
    /// <summary>弹幕接收停止。</summary>
    WebSocketReceivingStopped,
    /// <summary>弹幕服务器心跳超时。</summary>
    WebSocketHeartbeatTimeout,
    /// <summary>收到损坏的弹幕协议消息。</summary>
    WebSocketInvalidMessage,
    /// <summary>需要重连的连接已关闭。</summary>
    SocketConnectionClosed,
    /// <summary>历史弹幕请求的房间号无效。</summary>
    HistoryInvalidRoomId,
    /// <summary>历史弹幕请求返回业务失败。</summary>
    HistoryRequestFailed,
    /// <summary>红包请求的房间号无效。</summary>
    RedPocketInvalidRoomId,
    /// <summary>红包请求返回业务失败。</summary>
    RedPocketRequestFailed,
    /// <summary>查询关注直播间需要登录。</summary>
    RoomSearchFollowingLoginRequired,
    /// <summary>房间搜索的数字标识无效。</summary>
    RoomSearchInvalidIdentifier,
    /// <summary>房间搜索响应数据不完整。</summary>
    RoomSearchResultsMissing,
    /// <summary>房间搜索请求失败。</summary>
    RoomSearchFailed,
    /// <summary>浏览器未提供完整的 B 站登录凭据。</summary>
    BrowserLoginBilibiliCookiesMissing,
    /// <summary>获取弹幕连接配置时平台返回业务失败。</summary>
    WebSocketConfigurationRequestFailed,
    /// <summary>平台拒绝弹幕连接的入房鉴权。</summary>
    WebSocketAuthenticationFailed,
    /// <summary>弹幕连接的入房鉴权回复无效。</summary>
    WebSocketAuthenticationResponseInvalid,
    /// <summary>弹幕连接未在期限内完成入房鉴权。</summary>
    WebSocketAuthenticationTimeout
}
