using System.Threading.Channels;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class RuntimeWakeups
{
    private readonly Channel<bool> _commands = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Channel<bool> _outbox = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    public void Commands() => _commands.Writer.TryWrite(true);
    public void Outbox() => _outbox.Writer.TryWrite(true);
    public async Task WaitCommands(CancellationToken ct) => _ = await _commands.Reader.ReadAsync(ct);
    public async Task WaitOutbox(CancellationToken ct) => _ = await _outbox.Reader.ReadAsync(ct);
}
