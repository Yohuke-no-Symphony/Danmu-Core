using Yohuke.DanmuCore.Apis.Models.Bilibili;

namespace Yohuke.DanmuCore.Services;

/// <summary>可选的原始业务事件旁路，原有弹幕消费者无需实现。</summary>
public interface IBilibiliRawEventSource
{
    /// <summary>在类型分发之前发布已解码且命令有效的业务 JSON。</summary>
    event EventHandler<BilibiliRawBusinessEvent>? RawBusinessEventReceived;
}
