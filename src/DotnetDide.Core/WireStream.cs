using System.Threading.Channels;

namespace DotnetDide;

/// <summary>A single-consumer response with a bounded binary queue and cooperative cancellation.</summary>
public sealed class WireStream : IDisposable
{
    public const int ChunkBytes = 64 * 1024;
    private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly TaskCompletionSource<WireResponse> _headers = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<WireResponse> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cancel;
    private readonly CancellationTokenRegistration _registration;
    private long _bytes;
    internal WireStream(long maximumBytes, CancellationToken cancellationToken)
    {
        if (maximumBytes < 1 || maximumBytes > 512L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        MaximumBytes = maximumBytes;
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _registration = _cancel.Token.Register(() => {
            _headers.TrySetCanceled(_cancel.Token);
            _chunks.Writer.TryComplete(new OperationCanceledException(_cancel.Token));
        });
        Touch();
    }
    public long MaximumBytes { get; }
    public Task<WireResponse> Headers => _headers.Task;
    public Task<WireResponse> Completion => _completion.Task;
    internal CancellationToken Token => _cancel.Token;
    internal void Touch() => _cancel.CancelAfter(TimeSpan.FromSeconds(30));
    internal void Start(WireResponse response) { Token.ThrowIfCancellationRequested(); _headers.TrySetResult(response); Touch(); }
    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes)
    {
        if (_bytes + bytes.Length > MaximumBytes) throw new IOException($"Site response exceeds {MaximumBytes} bytes.");
        _bytes += bytes.Length;
        while (!bytes.IsEmpty)
        {
            Token.ThrowIfCancellationRequested();
            var length = Math.Min(bytes.Length, ChunkBytes);
            await _chunks.Writer.WriteAsync(bytes[..length].ToArray(), Token);
            bytes = bytes[length..]; Touch();
        }
    }
    /// <summary>Read the next chunk; an empty array means EOF. Only one reader is allowed.</summary>
    public async Task<byte[]> ReadAsync()
    {
        Token.ThrowIfCancellationRequested(); Touch();
        if (!await _chunks.Reader.WaitToReadAsync(Token)) return [];
        var value = await _chunks.Reader.ReadAsync(Token); Touch(); return value;
    }
    internal void Finish(WireResponse response)
    {
        var started = _headers.Task.IsCompletedSuccessfully;
        _headers.TrySetResult(response);
        _chunks.Writer.TryComplete(response.Error is not null && started ? new IOException(response.Error) : null);
        _completion.TrySetResult(response);
    }
    internal void Fail(Exception error)
    { _headers.TrySetException(error); _chunks.Writer.TryComplete(error); _completion.TrySetException(error); }
    public void Cancel() => _cancel.Cancel();
    public void Dispose() { _cancel.Cancel(); _registration.Dispose(); _cancel.Dispose(); }
}
