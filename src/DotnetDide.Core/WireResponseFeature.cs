using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
namespace DotnetDide;

internal sealed class WireResponseFeature : IHttpResponseFeature, IHttpResponseBodyFeature, IDisposable
{
    private readonly List<(Func<object, Task> Callback, object State)> _starting = [];
    private readonly List<(Func<object, Task> Callback, object State)> _completed = [];
    private readonly ResponseStream _stream;
    private PipeWriter? _writer;
    public WireResponseFeature() => _stream = new ResponseStream(this);
    public int StatusCode { get; set; } = 200;
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get => _stream; set => throw new NotSupportedException("Replace HttpResponse.Body through the body feature."); }
    public bool HasStarted { get; private set; }
    public Stream Stream => _stream;
    public PipeWriter Writer => _writer ??= PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
    internal long Length => _stream.Length;
    internal byte[] Bytes => _stream.ToArray();
    public void DisableBuffering() { } // Wire transports bounded complete responses.
    public void OnStarting(Func<object, Task> callback, object state)
    { if (HasStarted) throw new InvalidOperationException("Response already started."); _starting.Add((callback, state)); }
    public void OnCompleted(Func<object, Task> callback, object state) => _completed.Add((callback, state));
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (HasStarted) return; cancellationToken.ThrowIfCancellationRequested();
        while (_starting.Count > 0) { var last = _starting[^1]; _starting.RemoveAt(_starting.Count - 1); await last.Callback(last.State); }
        HasStarted = true; if (Headers is HeaderDictionary dictionary) dictionary.IsReadOnly = true;
    }
    public async Task CompleteAsync()
    { await StartAsync(); if (_writer is not null) await _writer.FlushAsync(); }
    internal async Task FinishCallbacksAsync()
    { while (_completed.Count > 0) { var last = _completed[^1]; _completed.RemoveAt(_completed.Count - 1); try { await last.Callback(last.State); } catch {/* Request teardown cannot replace the HTTP response. */} } }
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => throw new NotSupportedException("Wire serves session files through its webRoot; SendFileAsync is unavailable.");
    public void Dispose() { _writer?.Complete(); _stream.Dispose(); }
    private sealed class ResponseStream(WireResponseFeature owner) : MemoryStream
    {
        private void Check(int count) { if (Position + count > WireServer.MaximumBodyBytes) throw new IOException("Site response exceeds 2 MiB."); }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); Start(); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); Start(); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); Start(); base.WriteByte(value); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Check(buffer.Length); await owner.StartAsync(cancellationToken); base.Write(buffer.Span); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void SetLength(long value) { if (value > WireServer.MaximumBodyBytes) throw new IOException("Site response exceeds 2 MiB."); base.SetLength(value); }
        private void Start() { var task = owner.StartAsync(); if (!task.IsCompleted) throw new InvalidOperationException("Use asynchronous writes when OnStarting callbacks await."); task.GetAwaiter().GetResult(); }
    }
}
