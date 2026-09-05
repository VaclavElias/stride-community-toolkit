using Stride.Audio;
using System.Buffers.Binary;
using System.Text;

namespace Stride.CommunityToolkit.Audio;

/// <summary>
/// Decodes RIFF/WAVE to 16-bit interleaved PCM. See <see cref="WavSound"/> for the supported formats.
/// </summary>
internal static class WavReader
{
    private const ushort FormatPcm = 1;
    private const ushort FormatIeeeFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    private static readonly Dictionary<ushort, string> KnownCompressedFormats = new()
    {
        [0x0002] = "ADPCM",
        [0x0006] = "A-law",
        [0x0007] = "mu-law",
        [0x0011] = "IMA ADPCM",
        [0x0055] = "MP3",
    };

    public static WavSound Read(Stream stream, AudioEngine? engine)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        ExpectTag(reader, "RIFF", "not a RIFF file");
        reader.ReadUInt32();                                   // file size, unreliable and unused
        ExpectTag(reader, "WAVE", "a RIFF file but not a WAVE file");

        Format? format = null;
        byte[]? data = null;

        while (data is null && TryReadChunkHeader(reader, out var tag, out var size))
        {
            switch (tag)
            {
                case "fmt ":
                    format = ReadFormat(reader, size);
                    break;

                case "data":
                    if (format is null)
                        throw new InvalidDataException("WAVE data chunk before the fmt chunk.");

                    data = reader.ReadBytes(checked((int)size));
                    break;

                default:
                    Skip(reader, size);
                    break;
            }

            if (data is null && (size & 1) == 1)
                Skip(reader, 1);                               // chunks are word-aligned
        }

        if (format is null)
            throw new InvalidDataException("WAVE file has no fmt chunk.");

        if (data is null || data.Length == 0)
            throw new InvalidDataException("WAVE file has no data chunk, or it is empty.");

        var samples = ToPcm16(data, format.Value);

        return new WavSound(format.Value.SampleRate, format.Value.Channels, samples, engine);
    }

    private readonly record struct Format(ushort Tag, int Channels, int SampleRate, int BitsPerSample);

    private static Format ReadFormat(BinaryReader reader, uint size)
    {
        if (size < 16)
            throw new InvalidDataException($"WAVE fmt chunk is {size} bytes; at least 16 expected.");

        var tag = reader.ReadUInt16();
        var channels = reader.ReadUInt16();
        var sampleRate = reader.ReadInt32();
        reader.ReadInt32();                                    // byte rate
        reader.ReadUInt16();                                   // block align
        var bits = reader.ReadUInt16();
        var consumed = 16u;

        if (tag == FormatExtensible)
        {
            // cbSize, valid bits, channel mask, then the sub-format GUID whose first two bytes are the real tag.
            if (size < 40)
                throw new InvalidDataException("WAVE extensible fmt chunk is too short to carry its sub-format.");

            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadUInt32();
            tag = reader.ReadUInt16();
            Skip(reader, 14);
            consumed = 40;
        }

        Skip(reader, size - consumed);

        if (tag is not (FormatPcm or FormatIeeeFloat))
        {
            var name = KnownCompressedFormats.TryGetValue(tag, out var known) ? known : $"format tag 0x{tag:X4}";

            throw new InvalidDataException($"WAVE file holds {name}, which is not decoded here. Export it as PCM.");
        }

        if (channels is not (1 or 2))
            throw new InvalidDataException($"WAVE file has {channels} channels; only mono and stereo are supported.");

        if (sampleRate <= 0)
            throw new InvalidDataException($"WAVE file declares a sample rate of {sampleRate}.");

        var validBits = tag == FormatIeeeFloat ? bits == 32 : bits is 8 or 16 or 24 or 32;

        if (!validBits)
            throw new InvalidDataException($"WAVE file has {bits}-bit {(tag == FormatIeeeFloat ? "float" : "integer")} samples, which are not decoded here.");

        return new Format(tag, channels, sampleRate, bits);
    }

    private static short[] ToPcm16(byte[] data, Format format)
    {
        var bytesPerSample = format.BitsPerSample / 8;
        var count = data.Length / bytesPerSample;
        var samples = new short[count];
        var span = data.AsSpan();

        for (var i = 0; i < count; i++)
        {
            var at = span.Slice(i * bytesPerSample, bytesPerSample);

            samples[i] = format switch
            {
                { Tag: FormatIeeeFloat } => (short)Math.Clamp(BinaryPrimitives.ReadSingleLittleEndian(at) * short.MaxValue, short.MinValue, short.MaxValue),
                { BitsPerSample: 8 } => (short)((at[0] - 128) << 8),                                  // unsigned in WAVE
                { BitsPerSample: 16 } => BinaryPrimitives.ReadInt16LittleEndian(at),
                { BitsPerSample: 24 } => BinaryPrimitives.ReadInt16LittleEndian(at[1..]),             // the top two of three bytes
                _ => (short)(BinaryPrimitives.ReadInt32LittleEndian(at) >> 16),
            };
        }

        return samples;
    }

    private static bool TryReadChunkHeader(BinaryReader reader, out string tag, out uint size)
    {
        var bytes = reader.ReadBytes(8);

        if (bytes.Length < 8)
        {
            tag = string.Empty;
            size = 0;
            return false;
        }

        tag = Encoding.ASCII.GetString(bytes, 0, 4);
        size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        return true;
    }

    private static void ExpectTag(BinaryReader reader, string expected, string otherwise)
    {
        var bytes = reader.ReadBytes(4);

        if (bytes.Length < 4 || Encoding.ASCII.GetString(bytes) != expected)
            throw new InvalidDataException($"Not a WAVE file: {otherwise}.");
    }

    private static void Skip(BinaryReader reader, uint count)
    {
        if (reader.BaseStream.CanSeek)
            reader.BaseStream.Seek(count, SeekOrigin.Current);
        else
            reader.ReadBytes(checked((int)count));
    }
}