using System.Threading.Channels;
using Telegram.Bot.Types;

namespace BanterBotSports.Web.Telegram;

/// <summary>
/// Bounded channel used to decouple the webhook endpoint (producer)
/// from the update processing worker (consumer).
/// Uses Wait mode to avoid silent data loss — the webhook briefly blocks
/// rather than dropping prediction messages when the queue is full.
/// </summary>
public sealed class TelegramUpdateQueue
{
    private const int Capacity = 500;

    private readonly Channel<Update> _channel = Channel.CreateBounded<Update>(
        new BoundedChannelOptions(Capacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelWriter<Update> Writer => _channel.Writer;
    public ChannelReader<Update> Reader => _channel.Reader;
}
