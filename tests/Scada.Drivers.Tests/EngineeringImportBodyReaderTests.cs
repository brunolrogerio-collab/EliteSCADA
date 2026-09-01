using Microsoft.AspNetCore.Http;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class EngineeringImportBodyReaderTests
{
    [Fact]
    public async Task ReadJsonAsync_RoundTripsStrictUtf8()
    {
        var expected = "{\"name\":\"Pressão\"}";
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(expected));

        var actual = await EngineeringImportBodyReader.ReadJsonAsync(context.Request);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadCsvAsync_RejectsDeclaredOversizeBeforeReadingBody()
    {
        var body = new GeneratedStream(0);
        var context = new DefaultHttpContext();
        context.Request.ContentLength = EngineeringImportBodyReader.MaximumCsvBytes + 1L;
        context.Request.Body = body;

        var exception = await Assert.ThrowsAsync<EngineeringImportBodyTooLargeException>(() =>
            EngineeringImportBodyReader.ReadCsvAsync(context.Request));

        Assert.Equal(EngineeringImportBodyReader.MaximumCsvBytes, exception.LimitBytes);
        Assert.Equal(0, body.ReadCount);
    }

    [Fact]
    public async Task ReadCsvAsync_RejectsStreamThatExceedsMisleadingContentLength()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = 1;
        context.Request.Body = new GeneratedStream(EngineeringImportBodyReader.MaximumCsvBytes + 1L);

        var exception = await Assert.ThrowsAsync<EngineeringImportBodyTooLargeException>(() =>
            EngineeringImportBodyReader.ReadCsvAsync(context.Request));

        Assert.Equal(EngineeringImportBodyReader.MaximumCsvBytes, exception.LimitBytes);
    }

    [Fact]
    public async Task ReadJsonAsync_RejectsInvalidUtf8()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(new byte[] { 0xc3, 0x28 });

        await Assert.ThrowsAsync<EngineeringImportBodyEncodingException>(() =>
            EngineeringImportBodyReader.ReadJsonAsync(context.Request));
    }

    private sealed class GeneratedStream(long length) : Stream
    {
        private long _position;

        public int ReadCount { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = ReadCore(buffer.AsSpan(offset, count));
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ReadCore(buffer.Span));
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int ReadCore(Span<byte> buffer)
        {
            ReadCount++;
            var remaining = length - _position;
            if (remaining <= 0) return 0;
            var read = checked((int)Math.Min(buffer.Length, remaining));
            buffer[..read].Clear();
            _position += read;
            return read;
        }
    }
}
