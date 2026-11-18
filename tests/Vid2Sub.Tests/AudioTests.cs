using System.Buffers;
using Vid2Sub.Infrastructure.Audio;
using Vid2Sub.Infrastructure.Files;

namespace Vid2Sub.Tests;

public class AudioTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static float[] Tone(double hertz, int sampleRate, int samples) =>
        [.. Enumerable.Range(0, samples).Select(i => (float)Math.Sin(2 * Math.PI * hertz * i / sampleRate))];

    private static float[] Resample(float[] input, int sourceRate, int chunk = int.MaxValue)
    {
        var resampler = new Resampler(sourceRate, 16_000);
        var output = new ArrayBufferWriter<float>();
        for (var offset = 0; offset < input.Length; offset += Math.Min(chunk, input.Length - offset))
        {
            resampler.Write(input.AsSpan(offset, Math.Min(chunk, input.Length - offset)), output);
        }

        resampler.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    // Fixtures come from the media test set of cyaoc/subtitles. Sample counts are the source's duration
    // at 16 kHz; sources are 48 kHz except the 16 kHz 5.1 WAV.
    [Theory]
    [InlineData("mp4-aac-lc.mp4", 1707)]
    [InlineData("m4a-aac-lc.m4a", 1707)]
    [InlineData("mkv-aac-lc.mkv", 2048)]
    [InlineData("mp3.mp3", 1600)]
    [InlineData("wav-pcm.wav", 1600)]
    [InlineData("wav-5.1.wav", 320)]
    public async Task Supported_media_decodes_to_16kHz_mono(string fixture, int expectedSamples)
    {
        var samples = await new ManagedAudioProcessor().DecodeAsync(Fixture(fixture), TestContext.Current.CancellationToken);

        Assert.Equal(expectedSamples, samples.Length);
        Assert.InRange(samples.ToArray().Max(Math.Abs), 0.05f, 1f); // audible, not silence or garbage
        // Otherwise a directory scan silently skips a format we can decode.
        Assert.Contains(Path.GetExtension(fixture), InputFileCollector.MediaExtensions);
    }

    [Fact]
    public async Task Unsupported_codec_fails_with_the_supported_list()
    {
        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => new ManagedAudioProcessor().DecodeAsync(Fixture("ogg-vorbis.ogg"), TestContext.Current.CancellationToken));

        Assert.Contains("ogg-vorbis.ogg", error.Message);
        Assert.Contains("Supported:", error.Message);
    }

    [Theory]
    [InlineData(48_000)]
    [InlineData(44_100)]
    [InlineData(8_000)]
    public void Resampling_preserves_a_tone_below_the_new_nyquist(int sourceRate)
    {
        var output = Resample(Tone(1000, sourceRate, sourceRate), sourceRate);
        var expected = Tone(1000, 16_000, 16_000);

        Assert.Equal(expected.Length, output.Length);
        for (var i = 200; i < expected.Length - 200; i++) // edges ramp in and out of the surrounding silence
        {
            Assert.InRange(output[i] - expected[i], -0.01f, 0.01f);
        }
    }

    [Fact]
    public void Downsampling_removes_what_would_alias()
    {
        var output = Resample(Tone(10_000, 48_000, 48_000), 48_000); // 10 kHz cannot exist at 16 kHz

        Assert.InRange(output[200..^200].Max(Math.Abs), 0f, 0.01f); // the tone's abrupt start and end are broadband
    }

    [Fact]
    public void Feeding_in_chunks_gives_the_same_samples_as_feeding_at_once()
    {
        var input = Tone(440, 44_100, 44_100);

        Assert.Equal(Resample(input, 44_100), Resample(input, 44_100, chunk: 777));
    }

    [Fact]
    public void Audio_already_at_16kHz_passes_through_untouched()
    {
        var input = Tone(440, 16_000, 1000);

        Assert.Equal(input, Resample(input, 16_000));
    }

    [Fact]
    public void Surround_downmix_favours_the_centre_and_drops_the_lfe()
    {
        var weights = ManagedAudioProcessor.DownmixWeights(6); // FL FR FC LFE BL BR

        Assert.Equal(0f, weights[3]);
        Assert.True(weights[2] > weights[0] && weights[0] > weights[4]);
        Assert.Equal(1f, weights.Sum(), 4);
        Assert.Equal([0.5f, 0.5f], ManagedAudioProcessor.DownmixWeights(2));
    }
}
