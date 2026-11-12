using Yohuke.DanmuCore.Apis.Models.Bilibili;

namespace Yohuke.DanmuCore.Services;

/// <summary>按消息身份和出现次数消除历史与实时流之间的重复消息。</summary>
public sealed class DanmuHistoryReconciler
{
    private const int CAPACITY = 600;
    private readonly object gate = new();
    private readonly Queue<DanmuHistoryObservation> observations = new();

    /// <summary>接收实时消息，仅消费已经出现的一个历史副本。</summary>
    public bool AcceptLive(BilibiliDanmuMessage message)
    {
        lock (gate)
        {
            var history = observations.FirstOrDefault(item => item.IsHistory && !item.MatchedLive && Matches(item.Message, message));
            if (history is not null)
            {
                history.MatchedLive = true;
                return false;
            }

            Add(message, false);
            return true;
        }
    }

    /// <summary>接收历史消息，并保留未在已有消息中匹配的出现次数。</summary>
    public IReadOnlyList<BilibiliDanmuMessage> AcceptHistory(IEnumerable<BilibiliDanmuMessage> messages)
    {
        lock (gate)
        {
            var used = new HashSet<DanmuHistoryObservation>();
            var accepted = new List<BilibiliDanmuMessage>();
            foreach (var message in messages)
            {
                var match = observations.FirstOrDefault(item => !used.Contains(item) && Matches(item.Message, message));
                if (match is not null)
                {
                    used.Add(match);
                    continue;
                }

                var added = Add(message, true);
                used.Add(added);
                accepted.Add(message);
            }

            return accepted;
        }
    }

    /// <summary>清除当前房间的去重观察记录。</summary>
    public void Clear()
    {
        lock (gate)
        {
            observations.Clear();
        }
    }

    private DanmuHistoryObservation Add(BilibiliDanmuMessage message, bool history)
    {
        var observation = new DanmuHistoryObservation(message, history);
        observations.Enqueue(observation);
        while (observations.Count > CAPACITY)
        {
            observations.Dequeue();
        }

        return observation;
    }

    private static bool Matches(BilibiliDanmuMessage first, BilibiliDanmuMessage second)
    {
        if (first.RoomId != second.RoomId)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(first.MessageId) && !string.IsNullOrWhiteSpace(second.MessageId))
        {
            return first.MessageId == second.MessageId;
        }

        if (first.TimestampMilliseconds > 0 && second.TimestampMilliseconds > 0 && first.TimestampMilliseconds != second.TimestampMilliseconds)
        {
            return false;
        }
        return first.Timestamp > 0 && second.Timestamp == first.Timestamp && first.Uid > 0 &&
               first.Uid == second.Uid && first.Content == second.Content;
    }
}
