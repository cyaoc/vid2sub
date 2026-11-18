using System.Buffers;
using System.Numerics;

namespace Vid2Sub.Infrastructure.Audio;

/// <summary>
/// Streaming polyphase windowed-sinc resampler for one channel. The low-pass at the lower
/// Nyquist frequency keeps downsampling (48 kHz to 16 kHz) from aliasing.
/// </summary>
internal sealed class Resampler
{
    private const int ZeroCrossings = 16;

    private readonly int _up;            // output rate / input rate == _up / _down, reduced
    private readonly int _down;
    private readonly int _halfWidth;     // kernel reach on each side, in input samples
    private readonly float[][] _kernels; // one per output phase
    private float[] _pending;            // input not fully consumed yet
    private int _pendingCount;
    private long _pendingStart;          // input index of _pending[0]
    private long _nextOutput;

    public Resampler(int sourceRate, int targetRate)
    {
        var divisor = (int)BigInteger.GreatestCommonDivisor(sourceRate, targetRate);
        _up = targetRate / divisor;
        _down = sourceRate / divisor;

        var cutoff = 0.475 * Math.Min(1.0, (double)targetRate / sourceRate); // cycles per input sample
        _halfWidth = (int)Math.Ceiling(ZeroCrossings / (2 * cutoff));

        _kernels = new float[_up][];
        for (var phase = 0; phase < _up; phase++)
        {
            var kernel = new double[2 * _halfWidth];
            for (var tap = 0; tap < kernel.Length; tap++)
            {
                var distance = tap - _halfWidth + 1 - (double)phase / _up; // input samples from the output instant
                var x = Math.PI * 2 * cutoff * distance;
                var sinc = x == 0 ? 1 : Math.Sin(x) / x;
                var w = Math.PI * distance / _halfWidth;
                var blackman = Math.Abs(distance) < _halfWidth ? 0.42 + 0.5 * Math.Cos(w) + 0.08 * Math.Cos(2 * w) : 0;
                kernel[tap] = sinc * blackman;
            }

            var gain = kernel.Sum(); // normalize so a constant input keeps its level
            _kernels[phase] = [.. kernel.Select(value => (float)(value / gain))];
        }

        // Leading silence lets the first outputs reach "before" the start of the input.
        _pending = new float[4 * _halfWidth];
        _pendingCount = _halfWidth;
        _pendingStart = -_halfWidth;
    }

    /// <summary>Feeds input samples and appends every output sample they complete.</summary>
    public void Write(ReadOnlySpan<float> input, IBufferWriter<float> output)
    {
        if (_up == _down)
        {
            output.Write(input);
            return;
        }

        if (_pending.Length < _pendingCount + input.Length)
        {
            Array.Resize(ref _pending, Math.Max(_pendingCount + input.Length, 2 * _pending.Length));
        }

        input.CopyTo(_pending.AsSpan(_pendingCount));
        _pendingCount += input.Length;

        var available = _pendingStart + _pendingCount;
        var destination = output.GetSpan((int)((long)input.Length * _up / _down) + 2);
        var written = 0;
        long center;
        while ((center = _nextOutput * _down / _up) + _halfWidth < available)
        {
            var kernel = _kernels[(int)(_nextOutput * _down % _up)];
            var window = _pending.AsSpan((int)(center - _halfWidth + 1 - _pendingStart), kernel.Length);
            var sum = 0f;
            for (var tap = 0; tap < kernel.Length; tap++)
            {
                sum += window[tap] * kernel[tap];
            }

            destination[written++] = sum;
            _nextOutput++;
        }

        output.Advance(written);

        // Keep only the input the next output still reaches back to.
        var consumed = (int)(center - _halfWidth + 1 - _pendingStart);
        if (consumed > 0)
        {
            _pendingCount -= consumed;
            _pending.AsSpan(consumed, _pendingCount).CopyTo(_pending);
            _pendingStart += consumed;
        }
    }

    /// <summary>Emits the outputs still waiting for input that will never come.</summary>
    public void Flush(IBufferWriter<float> output)
    {
        if (_up != _down)
        {
            Write(new float[_halfWidth], output);
        }
    }
}
