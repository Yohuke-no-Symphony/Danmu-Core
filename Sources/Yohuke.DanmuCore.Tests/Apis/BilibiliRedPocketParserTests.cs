using Newtonsoft.Json.Linq;
using Yohuke.DanmuCore.Apis.Protocols.Bilibili;

namespace Yohuke.DanmuCore.Tests.Apis;

/// <summary>验证两端红包摘要与完整详情协议的合并。</summary>
public sealed class BilibiliRedPocketParserTests
{
    /// <summary>支持不同前缀和命令后缀，通知不会声称已取得完整详情。</summary>
    [Theory]
    [InlineData("POPULARITY_RED_POCKET_NEW")]
    [InlineData("POPULARITY_RED_POCKET_V2_NEW:1")]
    public void NotificationsKeepNestedDetailsAndDoNotClaimAResult(string command)
    {
        var json = JObject.Parse("""
            {"data":{"lot_id":42,"sender_name":"外层用户","lot_info":{
              "lot_id":42,"sender_uid":4294967342,"sender_name":"内层用户",
              "rp_type":3,"total_battery":2500,"current_time":1800000000123,
              "sender_uinfo":{"base":{"face":"https://example.com/avatar.png","is_mystery":true}}
            }}}
            """);
        json["cmd"] = command;

        var message = BilibiliRedPocketMessageParser.Parse("100", json);

        Assert.NotNull(message);
        Assert.Equal("100", message.RoomId);
        Assert.Equal(42, message.RedPocket.LotId);
        Assert.Equal(4294967342, message.RedPocket.SenderUid);
        Assert.Equal("外层用户", message.RedPocket.SenderName);
        Assert.Equal("https://example.com/avatar.png", message.RedPocket.SenderAvatarUrl);
        Assert.True(message.RedPocket.IsMystery);
        Assert.Equal(3, message.RedPocket.Type);
        Assert.Equal(2500m, message.RedPocket.TotalBattery);
        Assert.Equal(1800000000, message.Timestamp);
        Assert.False(message.IsDrawn);
        Assert.False(message.RedPocket.HasDetails);
        Assert.False(message.RedPocket.HasResult);
    }

    /// <summary>开奖命令即使没有中奖名单也保留开奖状态及嵌套红包身份。</summary>
    [Fact]
    public void DrawnStateIsIndependentOfWinnerAvailability()
    {
        var json = JObject.Parse("""
            {"cmd":"POPULARITY_RED_POCKET_WINNER_LIST","send_time":1800000000,
             "data":{"lot_info":{"lot_id":42,"sender_uid":9,"sender_name":"用户"}}}
            """);

        var message = BilibiliRedPocketMessageParser.Parse("100", json);

        Assert.NotNull(message);
        Assert.True(message.IsDrawn);
        Assert.False(message.RedPocket.HasResult);
        Assert.True(message.RedPocket.HasDetails);
        Assert.Empty(message.RedPocket.Winners);
        Assert.Equal(42, message.RedPocket.LotId);
        Assert.Equal(1800000000, message.Timestamp);
    }

    /// <summary>外层通知、详情与补查身份必须一致。</summary>
    [Theory]
    [InlineData("{\"lot_id\":42,\"lot_info\":{\"lot_id\":43}}", 0)]
    [InlineData("{\"lot_id\":42,\"lot_info\":{\"lot_id\":42}}", 43)]
    public void MismatchedIdentitiesAreRejected(string data, long requestedLotId)
    {
        Assert.Throws<InvalidDataException>(() =>
            BilibiliRedPocketMessageParser.ParseInfo("100", JObject.Parse(data), requestedLotId));
        Assert.Throws<InvalidDataException>(() =>
            BilibiliRedPocketMessageParser.ParseWinners("100", requestedLotId, JObject.Parse(data)));
    }

    /// <summary>奖品与中奖用户详情继续保留，开奖消息不降为摘要。</summary>
    [Fact]
    public void WinnersKeepAwardAndSenderDetails()
    {
        var data = JObject.Parse("""
            {"lot_id":42,"rp_type":3,"lot_info":{"lot_id":42,"sender_name":"发包人"},
             "awards":{"8":{"award_name":"测试奖品","award_pic":"https://example.com/gift.png","award_price":100}},
             "winner_info":[{"uid":7,"uname":"中奖用户","gift_id":"8","battery_amount":25}]}
            """);

        var message = BilibiliRedPocketMessageParser.ParseWinners("100", 42, data);

        Assert.True(message.IsDrawn);
        Assert.True(message.RedPocket.HasResult);
        Assert.Equal("发包人", message.RedPocket.SenderName);
        var winner = Assert.Single(message.RedPocket.Winners);
        Assert.Equal(7, winner.Uid);
        Assert.Equal("中奖用户", winner.UserName);
        Assert.Equal("测试奖品", winner.GiftName);
        Assert.Equal("https://example.com/gift.png", winner.GiftImageUrl);
        Assert.Equal(25m, winner.BatteryAmount);
    }
}
