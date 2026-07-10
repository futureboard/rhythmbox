using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;

namespace Rythmbox.Core.Samples;

/// <summary>
/// Metadata for a PCM WAV file whose audio is read from an OS memory mapping.
/// The descriptor itself owns no file handle; a mapping is opened only for the
/// playback lifetime, so switching kits promptly releases its OS resources.
/// </summary>
public sealed class MemoryMappedWavSample
{
    private const short WaveFormatPcm = 1;
    private const short WaveFormatIeeeFloat = 3;
    private const short WaveFormatExtensible = unchecked((short)0xFFFE);

    private static readonly Guid SubFormatPcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubFormatFloat = new("00000003-0000-0010-8000-00AA00389B71");

    private MemoryMappedWavSample(
        string filePath,
        long fileLength,
        long dataOffset,
        int dataLength,
        int channels,
        int sampleRate,
        int blockAlign,
        int bitsPerSample,
        bool isFloat)
    {
        FilePath = filePath;
        FileLength = fileLength;
        DataOffset = dataOffset;
        DataLength = dataLength;
        Channels = channels;
        SampleRate = sampleRate;
        BlockAlign = blockAlign;
        BitsPerSample = bitsPerSample;
        IsFloat = isFloat;
        FrameCount = dataLength / blockAlign;
    }

    public string FilePath { get; }

    /// <summary>Total bytes represented by this mapping, used for load budgeting.</summary>
    public long FileLength { get; }

    public int FrameCount { get; }

    public int SampleRate { get; }

    internal long DataOffset { get; }

    internal int DataLength { get; }

    internal int Channels { get; }

    internal int BlockAlign { get; }

    internal int BitsPerSample { get; }

    internal bool IsFloat { get; }

    public IPlaybackSample CreatePlaybackSample() => new MappedPlaybackSample(this);

    /// <summary>
    /// Materializes this source only when an editor explicitly needs writable PCM.
    /// Runtime kit loading uses <see cref="CreatePlaybackSample"/> and does not call this.
    /// </summary>
    public float[] DecodeMono(int targetSampleRate = WavCodec.TargetSampleRate)
    {
        using var source = CreatePlaybackSample();
        var mono = new float[source.FrameCount];
        for (var i = 0; i < mono.Length; i++)
        {
            mono[i] = source.ReadFrame(i);
        }

        return source.SampleRate == targetSampleRate
            ? mono
            : WavResampler.Resample(mono, source.SampleRate, targetSampleRate);
    }

    /// <summary>
    /// Faults in the leading pages of a sample while the preset is loading in
    /// the background. Drum attacks then avoid a cold page fault on the audio
    /// callback, while long tails remain lazily paged by the operating system.
    /// </summary>
    public void WarmStart(int maximumFrames = 4_096)
    {
        if (FrameCount == 0 || maximumFrames <= 0)
        {
            return;
        }

        using var source = CreatePlaybackSample();
        var frameLimit = Math.Min(FrameCount, maximumFrames);
        var framesPerPage = Math.Max(1, 4_096 / BlockAlign);
        for (var frame = 0; frame < frameLimit; frame += framesPerPage)
        {
            _ = source.ReadFrame(frame);
        }

        _ = source.ReadFrame(frameLimit - 1);
    }

    public static MemoryMappedWavSample Open(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("WAV sample was not found.", fullPath);
        }

        if (info.Length < 44)
        {
            throw new InvalidDataException("WAV file is too small.");
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);

        Span<byte> riffHeader = stackalloc byte[12];
        ReadExactly(stream, riffHeader);
        if (!riffHeader[..4].SequenceEqual("RIFF"u8) || !riffHeader.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        WavFormat? format = null;
        long dataOffset = 0;
        var dataLength = 0;
        Span<byte> chunkHeader = stackalloc byte[8];

        while (stream.Position + chunkHeader.Length <= stream.Length)
        {
            ReadExactly(stream, chunkHeader);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(4, 4));
            var chunkDataOffset = stream.Position;
            var paddedChunkSize = (long)chunkSize + (chunkSize & 1);

            if (chunkDataOffset + paddedChunkSize > stream.Length)
            {
                throw new InvalidDataException("WAV chunk exceeds file length.");
            }

            if (chunkHeader[..4].SequenceEqual("fmt "u8))
            {
                if (chunkSize is < 16 or > 65_536)
                {
                    throw new InvalidDataException("WAV fmt chunk has an invalid size.");
                }

                var fmtBytes = new byte[(int)chunkSize];
                ReadExactly(stream, fmtBytes);
                format = ParseFormat(fmtBytes);
            }
            else if (chunkHeader[..4].SequenceEqual("data"u8))
            {
                if (chunkSize > int.MaxValue)
                {
                    throw new InvalidDataException("WAV data chunk is too large.");
                }

                dataOffset = chunkDataOffset;
                dataLength = (int)chunkSize;
                break;
            }
            else
            {
                stream.Seek(paddedChunkSize, SeekOrigin.Current);
                continue;
            }

            stream.Seek(chunkDataOffset + paddedChunkSize, SeekOrigin.Begin);
        }

        if (format is null || dataLength == 0)
        {
            throw new InvalidDataException("WAV file does not contain a supported data chunk.");
        }

        var wavFormat = format.Value;
        if (dataLength < wavFormat.BlockAlign)
        {
            throw new InvalidDataException("WAV data chunk is shorter than one frame.");
        }

        return new MemoryMappedWavSample(
            fullPath,
            info.Length,
            dataOffset,
            dataLength,
            wavFormat.Channels,
            wavFormat.SampleRate,
            wavFormat.BlockAlign,
            wavFormat.BitsPerSample,
            wavFormat.IsFloat);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of WAV file.");
            }

            offset += read;
        }
    }

    private static WavFormat ParseFormat(ReadOnlySpan<byte> bytes)
    {
        var formatTag = BinaryPrimitives.ReadInt16LittleEndian(bytes);
        var channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2, 2));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
        var blockAlign = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(12, 2));
        var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(14, 2));
        var isFloat = formatTag == WaveFormatIeeeFloat;

        if (formatTag == WaveFormatExtensible && bytes.Length >= 40)
        {
            bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(18, 2));
            var subFormat = new Guid(bytes.Slice(24, 16));
            isFloat = subFormat == SubFormatFloat;
            if (subFormat != SubFormatPcm && subFormat != SubFormatFloat)
            {
                throw new NotSupportedException($"Unsupported WAV sub-format {subFormat}.");
            }
        }
        else if (formatTag != WaveFormatPcm && formatTag != WaveFormatIeeeFloat)
        {
            throw new NotSupportedException($"Unsupported WAV format tag {formatTag}.");
        }

        if (channels <= 0 || sampleRate <= 0 || blockAlign <= 0)
        {
            throw new InvalidDataException("WAV format contains invalid channel, sample-rate, or block-alignment data.");
        }

        var bytesPerSample = blockAlign / channels;
        if (bytesPerSample <= 0 || bitsPerSample is not (8 or 16 or 24 or 32) || (isFloat && bitsPerSample != 32))
        {
            throw new NotSupportedException($"Unsupported WAV encoding ({bitsPerSample}-bit).");
        }

        return new WavFormat(channels, sampleRate, blockAlign, bitsPerSample, isFloat);
    }

    private readonly record struct WavFormat(int Channels, int SampleRate, short BlockAlign, short BitsPerSample, bool IsFloat);

    private sealed unsafe class MappedPlaybackSample : IPlaybackSample
    {
        private readonly MemoryMappedFile _mappedFile;
        private readonly MemoryMappedViewAccessor _view;
        private byte* _basePointer;
        private bool _disposed;

        public MappedPlaybackSample(MemoryMappedWavSample descriptor)
        {
            Descriptor = descriptor;
            _mappedFile = MemoryMappedFile.CreateFromFile(
                descriptor.FilePath,
                FileMode.Open,
                mapName: null,
                capacity: 0,
                MemoryMappedFileAccess.Read);
            _view = _mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            byte* pointer = null;
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            _basePointer = pointer + _view.PointerOffset;
        }

        private MemoryMappedWavSample Descriptor { get; }

        public int FrameCount => Descriptor.FrameCount;

        public int SampleRate => Descriptor.SampleRate;

        public float ReadFrame(int frameIndex)
        {
            if (_disposed || (uint)frameIndex >= (uint)Descriptor.FrameCount)
            {
                return 0f;
            }

            var frameOffset = Descriptor.DataOffset + ((long)frameIndex * Descriptor.BlockAlign);
            var bytesPerSample = Descriptor.BlockAlign / Descriptor.Channels;
            var sum = 0f;

            for (var channel = 0; channel < Descriptor.Channels; channel++)
            {
                sum += ReadSample(_basePointer + frameOffset + (channel * bytesPerSample), Descriptor, bytesPerSample);
            }

            return sum / Descriptor.Channels;
        }

        private static float ReadSample(byte* source, MemoryMappedWavSample descriptor, int bytesPerSample)
        {
            if (descriptor.IsFloat)
            {
                var bits = source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24);
                return BitConverter.Int32BitsToSingle(bits);
            }

            return descriptor.BitsPerSample switch
            {
                8 => (source[0] - 128) / 128f,
                16 => (short)(source[0] | (source[1] << 8)) / (float)short.MaxValue,
                24 => ReadInt24(source) / 8_388_608f,
                32 => (source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24)) / (float)int.MaxValue,
                _ => throw new NotSupportedException($"Unsupported WAV bit depth ({descriptor.BitsPerSample}-bit; {bytesPerSample} bytes/sample)."),
            };
        }

        private static int ReadInt24(byte* source)
        {
            var value = source[0] | (source[1] << 8) | (source[2] << 16);
            return (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePointer = null;
            _view.Dispose();
            _mappedFile.Dispose();
        }
    }
}
