using Newtonsoft.Json.Linq;
using Yohuke.DanmuCore.Apis.Models.Bilibili;
using Yohuke.DanmuCore.Apis.Protocols.Bilibili;

namespace Yohuke.DanmuCore.Apis;

/// <summary>将归档的原始业务包映射为共享平台消息模型。</summary>
public static class BilibiliBusinessEventParser
{
    /// <summary>解析旧版或新版批量礼物。</summary>
    public static IReadOnlyList<BilibiliGiftMessage> ParseGifts(string roomId, JObject json) => BilibiliGiftMessageParser.Parse(roomId, json).ToArray();
    /// <summary>解析红包内容和开奖事件。</summary>
    public static BilibiliRedPocketMessage? ParseRedPocket(string roomId, JObject json) => BilibiliRedPocketMessageParser.Parse(roomId, json);
}
