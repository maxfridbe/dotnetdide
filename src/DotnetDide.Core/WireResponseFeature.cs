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
    private readonly WireStream? _transport;
    internal bool SuppressBody { get; }
    internal long MaximumBytes => _transport?.MaximumBytes ?? WireServer.MaximumBodyBytes;
    public WireResponseFeature(WireStream? transport = null, bool suppressBody = false)
    { _transport = transport; SuppressBody = suppressBody; _stream = new ResponseStream(this); }
    public int StatusCode { get; set; } = 200;
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get => _stream; set => throw new NotSupportedException("Replace HttpResponse.Body through the body feature."); }
    public bool HasStarted { get; private set; }
    public Stream Stream => _stream;
    public PipeWriter Writer => _writer ??= PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
    internal long Length => _stream.Written;
    internal byte[] Bytes => _stream.ToArray();
    public void DisableBuffering() { } // Async writes already flow through the bounded transport.
    public void OnStarting(Func<object, Task> callback, object state)
    { if (HasStarted) throw new InvalidOperationException("Response already started."); _starting.Add((callback, state)); }
    public void OnCompleted(Func<object, Task> callback, object state) => _completed.Add((callback, state));
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (HasStarted) return; cancellationToken.ThrowIfCancellationRequested();
        while (_starting.Count > 0) { var last = _starting[^1]; _starting.RemoveAt(_starting.Count - 1); await last.Callback(last.State); }
        HasStarted = true; if (Headers is HeaderDictionary dictionary) dictionary.IsReadOnly = true;
        var headers = Headers.Where(pair => !pair.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Select(value => value ?? "").ToArray(), StringComparer.OrdinalIgnoreCase);
        _transport?.Start(new(StatusCode, headers, ""));
    }
    public async Task CompleteAsync()
    { await StartAsync(); if (_writer is not null) await _writer.FlushAsync(); await _stream.FlushAsync(); }
    internal async Task FinishCallbacksAsync()
    { while (_completed.Count > 0) { var last = _completed[^1]; _completed.RemoveAt(_completed.Count - 1); try { await last.Callback(last.State); } catch {/* Request teardown cannot replace the HTTP response. */} } }
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => throw new NotSupportedException("Wire serves session files through its webRoot; SendFileAsync is unavailable.");
    public void Dispose() { _writer?.Complete(); _stream.Dispose(); }
    private sealed class ResponseStream(WireResponseFeature owner) : Stream
    {
        private readonly MemoryStream _buffer = new();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Written;
        public override long Position { get => Written; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        internal byte[] ToArray() => _buffer.ToArray();
        protected override void Dispose(bool disposing) { if (disposing) _buffer.Dispose(); base.Dispose(disposing); }
        internal long Written { get; private set; }
        private void Check(int count)
        {
            if (Written + count > owner.MaximumBytes) throw new IOException($"Site response exceeds {owner.MaximumBytes} bytes.");
        }
        private bool Discard => owner.SuppressBody || owner.StatusCode is 204 or 205 or 304;
        private void WriteSync(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length); Start(); if (Discard) return;
            if (owner._transport is not null && _buffer.Length + buffer.Length > WireStream.ChunkBytes)
                throw new IOException("Use WriteAsync/FlushAsync for streaming writes beyond the 64 KiB synchronous buffer.");
            _buffer.Write(buffer); Written += buffer.Length;
        }
        public override void Write(byte[] buffer, int offset, int count) => WriteSync(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) => WriteSync(buffer);
        public override void WriteByte(byte value) => WriteSync(new ReadOnlySpan<byte>(in value));
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Check(buffer.Length); await owner.StartAsync(cancellationToken); if (Discard) return;
            if (owner._transport is null) { _buffer.Write(buffer.Span); }
            else { await FlushAsync(cancellationToken); await owner._transport.WriteAsync(buffer); }
            Written += buffer.Length;
        }
        public override async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); await owner.StartAsync(cancellationToken);
            if (owner._transport is not null && _buffer.Length > 0)
            { await owner._transport.WriteAsync(_buffer.GetBuffer().AsMemory(0, checked((int)_buffer.Length))); _buffer.SetLength(0); _buffer.Position = 0; }
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void SetLength(long value) => throw new NotSupportedException("HTTP response streams are not seekable.");
        public override void Flush()
        {
            if (owner._transport is not null && _buffer.Length > 0) throw new NotSupportedException("Use FlushAsync for streamed responses.");
        }
        private void Start() { var task = owner.StartAsync(); if (!task.IsCompleted) throw new InvalidOperationException("Use asynchronous writes when OnStarting callbacks await."); task.GetAwaiter().GetResult(); }
    }
}
