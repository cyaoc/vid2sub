using Vid2Sub.Domain.Enums;
using Vid2Sub.Infrastructure.Speech;

namespace Vid2Sub.Tests;

public class WhisperModelProviderTests
{
    [Fact]
    public async Task Missing_model_is_downloaded_once_and_then_reused()
    {
        using var temp = new TempDirectory();
        var downloads = 0;
        var provider = new WhisperModelProvider(temp.Path, download: (_, _) =>
        {
            downloads++;
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        });

        var first = await provider.EnsureModelAsync(ModelType.Tiny, TestContext.Current.CancellationToken);
        var second = await provider.EnsureModelAsync(ModelType.Tiny, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(temp.Path, "ggml-tiny.bin"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, downloads);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(first, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Interrupted_download_leaves_nothing_that_looks_like_a_model()
    {
        using var temp = new TempDirectory();
        var provider = new WhisperModelProvider(temp.Path, download: (_, _) => Task.FromResult<Stream>(new BrokenStream()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnsureModelAsync(ModelType.Base, TestContext.Current.CancellationToken));

        Assert.Contains("Base", error.Message);
        Assert.Empty(Directory.EnumerateFiles(temp.Path));
    }

    /// <summary>Yields a few bytes, then fails like a dropped connection.</summary>
    private sealed class BrokenStream : MemoryStream
    {
        private bool _started;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_started)
            {
                throw new IOException("connection reset");
            }

            _started = true;
            buffer.Span[0] = 42;
            return ValueTask.FromResult(1);
        }
    }
}
