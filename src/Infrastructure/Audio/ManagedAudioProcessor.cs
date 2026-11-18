using System.Buffers;
using ManagedCodecs;
using ManagedCodecs.Aac;
using ManagedCodecs.Containers;
using ManagedCodecs.Mp3;
using Vid2Sub.Domain.Interfaces;

namespace Vid2Sub.Infrastructure.Audio;

/// <summary>
/// Decodes media in-process with the pure managed ManagedCodecs library (no FFmpeg, no temporary
/// files): first audio track, downmixed to mono, resampled to 16 kHz.
/// </summary>
public sealed class ManagedAudioProcessor : IAudioProcessor
{
    private const int WhisperSampleRate = 16_000;

    public Task<ReadOnlyMemory<float>> DecodeAsync(string inputPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Decode(inputPath, cancellationToken), cancellationToken);

    private static ReadOnlyMemory<float> Decode(string inputPath, CancellationToken cancellationToken)
    {
        try
        {
            using var demuxer = Demuxer.Open(inputPath);

            // shortcut: always the first audio track, add a track option when a file needs another one
            var audio = demuxer.Audio ?? throw new NotSupportedException("the file has no audio track");
            // A new codec also needs the "Supported:" list below, InputFileCollector.MediaExtensions and README.
            ManagedCodecs.IAudioDecoder decoder = audio.Codec switch
            {
                CodecIds.Aac => new AacDecoder(audio),
                CodecIds.Mp3 => new Mp3Decoder(audio),
                var codec when PcmDecoder.CanDecode(codec) => new PcmDecoder(audio),
                var codec => throw new NotSupportedException($"audio codec '{codec}' is not supported")
            };

            // Sized from the container's duration so a long film does not regrow (and briefly double) the buffer.
            var expectedSamples = audio.Duration * audio.TimeBase.ToDouble() * WhisperSampleRate * 1.01;
            var output = new ArrayBufferWriter<float>((int)Math.Clamp(expectedSamples, 1, Array.MaxLength));
            Resampler? resampler = null;
            float[] weights = [];
            float[] mono = [];

            foreach (var packet in demuxer.ReadPackets())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (packet.StreamIndex != audio.StreamIndex)
                {
                    continue;
                }

                var frame = decoder.Decode(packet);
                if (frame is null || frame.SampleCount == 0)
                {
                    continue;
                }

                if (resampler is null)
                {
                    resampler = new Resampler(frame.SampleRate, WhisperSampleRate);
                    weights = DownmixWeights(frame.Channels);
                }

                if (mono.Length < frame.SampleCount)
                {
                    mono = new float[frame.SampleCount];
                }

                var block = mono.AsSpan(0, frame.SampleCount);
                block.Clear();
                for (var channel = 0; channel < weights.Length; channel++)
                {
                    var samples = frame.Samples[channel];
                    var weight = weights[channel];
                    for (var i = 0; i < block.Length; i++)
                    {
                        block[i] += samples[i] * weight;
                    }
                }

                resampler.Write(block, output);
            }

            resampler?.Flush(output);
            return output.WrittenMemory;
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException(
                $"Cannot decode '{Path.GetFileName(inputPath)}': {ex.Message.TrimEnd('.')}. " +
                "Supported: MP4, MOV, M4A, MKV, AVI, WAV, MP3 and AAC files with AAC, MP3 or PCM audio.",
                ex);
        }
    }

    /// <summary>
    /// Mono mix weights for channels in WAVE order (FL FR FC LFE ...): dialogue-heavy centre at full
    /// weight, fronts at -3 dB, surrounds lower, LFE dropped. Other layouts are a plain mean.
    /// </summary>
    internal static float[] DownmixWeights(int channels)
    {
        const float front = 0.7071f, surround = 0.5f, surround7 = 0.3536f;
        float[] weights = channels switch
        {
            3 => [front, front, 1],
            5 => [front, front, 1, surround, surround],
            6 => [front, front, 1, 0, surround, surround],
            7 => [front, front, 1, surround7, surround7, surround7, surround7],
            8 => [front, front, 1, 0, surround7, surround7, surround7, surround7],
            _ => [.. Enumerable.Repeat(1f, channels)]
        };

        var sum = weights.Sum();
        return [.. weights.Select(weight => weight / sum)];
    }
}
