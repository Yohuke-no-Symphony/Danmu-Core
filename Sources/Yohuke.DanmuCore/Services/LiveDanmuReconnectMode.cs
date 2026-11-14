namespace Yohuke.DanmuCore.Services;

/// <summary>定义弹幕连接失败后由共享客户端还是调用方接管重连。</summary>
public enum LiveDanmuReconnectMode
{
    /// <summary>使用共享客户端的退避策略持续重连。</summary>
    Automatic,
    /// <summary>单次连接失败后保留原异常，由调用方选择身份并重连。</summary>
    CallerManaged
}
