using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;

namespace Rythmbox.Core.Formats;

/// <summary>
/// RhythmLive drum kit archive (<c>.apak</c>).
/// Binary layout v1:
///   Header (48 bytes): magic, version, flags, IV[16], payload length u64
///   Payload: AES-256-CBC encrypted inner container when flag bit 0 set; otherwise plain inner bytes
/// Inner container (never compressed):
///   kit JSON length u32 + UTF-8 JSON (DrumStage-compatible; samples use <c>@N</c> asset refs)
///   asset count u32
///   repeat: name length u32, name UTF-8, data length u32, raw bytes (typically WAV)
/// </summary>
public static class ApakCodec
{
    public const string Extension = ".apak";
    public const string Magic = "APAK";
    public const ushort Version = 1;
    public const uint FlagEncrypted = 1;

    private const int HeaderSize = 48;

    public sealed class PackOptions
    {
        public byte[]? Key32 { get; init; }

        public bool Encrypt => Key32 is { Length: RhythmAes256.KeySizeBytes };
    }

    public static void Save(KitPreset kit, string outputPath, PackOptions? options = null)
    {
        options ??= new PackOptions { Key32 = RhythmAes256.FactoryKey };

        var assets = new List<(string Name, byte[] Data)>();
        var padsArray = new JsonArray();

        foreach (var pad in kit.Pads)
        {
            string? sampleRef = null;
            if (pad.HasAudio)
            {
                var assetName = SanitizeAssetName($"{pad.Label}_{pad.MidiNote}.wav");
                var wavBytes = WavCodec.EncodeMono(pad.Samples, pad.SampleRate);
                assets.Add((assetName, wavBytes));
                sampleRef = $"@{assets.Count - 1}";
            }
            else if (pad.FilePath is { Length: > 0 } && File.Exists(pad.FilePath))
            {
                var assetName = SanitizeAssetName(Path.GetFileName(pad.FilePath));
                assets.Add((assetName, File.ReadAllBytes(pad.FilePath)));
                sampleRef = $"@{assets.Count - 1}";
            }

            var padObj = new JsonObject
            {
                ["label"] = pad.Label,
                ["gain"] = pad.Gain,
            };

            if (pad.MidiNote >= 0)
            {
                padObj["midi_note"] = pad.MidiNote;
            }

            if (pad.ChokeGroup > 0)
            {
                padObj["choke_group"] = pad.ChokeGroup;
            }

            if (sampleRef is not null)
            {
                padObj["sample"] = sampleRef;
            }

            padsArray.Add(padObj);
        }

        var manifest = new JsonObject
        {
            ["name"] = kit.Name,
            ["pads"] = padsArray,
        };

        var inner = BuildInnerContainer(manifest.ToJsonString(), assets);
        WriteFile(outputPath, inner, options);
    }

    public static KitPreset Load(string apakPath, ReadOnlySpan<byte> key32)
    {
        var inner = ReadInnerPayload(File.ReadAllBytes(apakPath), key32);
        return ParseInnerContainer(inner);
    }

    public static KitPreset LoadFactory(string apakPath) => Load(apakPath, RhythmAes256.FactoryKey);

    private static byte[] BuildInnerContainer(string manifestJson, IReadOnlyList<(string Name, byte[] Data)> assets)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        writer.Write(manifestBytes.Length);
        writer.Write(manifestBytes);
        writer.Write(assets.Count);

        foreach (var (name, data) in assets)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(data.Length);
            writer.Write(data);
        }

        return stream.ToArray();
    }

    private static KitPreset ParseInnerContainer(ReadOnlySpan<byte> inner)
    {
        using var stream = new MemoryStream(inner.ToArray());
        using var reader = new BinaryReader(stream);

        var jsonLength = reader.ReadInt32();
        if (jsonLength < 0 || jsonLength > inner.Length)
        {
            throw new FormatException("APAK JSON length exceeds payload.");
        }

        var jsonBytes = reader.ReadBytes(jsonLength);
        var jsonText = Encoding.UTF8.GetString(jsonBytes);

        var assetCount = reader.ReadInt32();
        if (assetCount < 0)
        {
            throw new FormatException("APAK asset count is invalid.");
        }

        var assets = new List<byte[]>(assetCount);
        for (var i = 0; i < assetCount; i++)
        {
            var nameLength = reader.ReadInt32();
            if (nameLength < 0)
            {
                throw new FormatException("APAK asset name length is invalid.");
            }

            reader.ReadBytes(nameLength);
            var dataLength = reader.ReadInt32();
            if (dataLength < 0)
            {
                throw new FormatException("APAK asset data length is invalid.");
            }

            assets.Add(reader.ReadBytes(dataLength));
        }

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        var kit = new KitPreset
        {
            Name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Kit" : "Kit",
        };

        if (!root.TryGetProperty("pads", out var padsEl) || padsEl.ValueKind != JsonValueKind.Array)
        {
            return KitPresetCodec.CreateDefaultGmKit();
        }

        kit.Pads.Clear();
        foreach (var padEl in padsEl.EnumerateArray())
        {
            var sample = new DrumSample
            {
                Label = padEl.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "Pad" : "Pad",
                Gain = padEl.TryGetProperty("gain", out var gainEl) && gainEl.TryGetSingle(out var g) ? g : 1f,
                PitchSemitones = 0f,
                MidiNote = padEl.TryGetProperty("midi_note", out var noteEl) && noteEl.ValueKind == JsonValueKind.Number
                    ? noteEl.GetInt32()
                    : -1,
                ChokeGroup = padEl.TryGetProperty("choke_group", out var chokeEl) && chokeEl.ValueKind == JsonValueKind.Number
                    ? chokeEl.GetInt32()
                    : 0,
            };

            if (padEl.TryGetProperty("sample", out var sampleEl) && sampleEl.GetString() is { } sampleRef
                && sampleRef.StartsWith('@')
                && int.TryParse(sampleRef.AsSpan(1), out var assetIndex)
                && assetIndex >= 0
                && assetIndex < assets.Count)
            {
                var wavBytes = assets[assetIndex];
                sample.Samples = WavCodec.DecodeMono(wavBytes, out _);
                sample.SampleRate = WavCodec.TargetSampleRate;
            }

            kit.Pads.Add(sample);
        }

        if (kit.Pads.Count < GmPercussionMap.Pads.Count)
        {
            while (kit.Pads.Count < GmPercussionMap.Pads.Count)
            {
                var padIndex = kit.Pads.Count;
                var gmPad = GmPercussionMap.Pads[padIndex];
                kit.Pads.Add(new DrumSample { Label = gmPad.Label, MidiNote = gmPad.Note });
            }
        }

        return kit;
    }

    private static void WriteFile(string outputPath, byte[] innerPlain, PackOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        byte[] payload;
        byte[] iv = new byte[RhythmAes256.IvSizeBytes];
        uint flags = 0;

        if (options.Encrypt)
        {
            flags = FlagEncrypted;
            payload = RhythmAes256.Encrypt(innerPlain, options.Key32!, out iv);
        }
        else
        {
            payload = innerPlain;
        }

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        var header = new byte[HeaderSize];
        BinaryFormatWriter.WriteAscii(header.AsSpan(0, 4), Magic);
        BinaryFormatWriter.WriteUInt16LE(header.AsSpan(4), Version);
        BinaryFormatWriter.WriteUInt16LE(header.AsSpan(6), 0);
        BinaryFormatWriter.WriteUInt32LE(header.AsSpan(8), flags);
        iv.CopyTo(header.AsSpan(12));
        BinaryFormatWriter.WriteUInt64LE(header.AsSpan(28), (ulong)payload.Length);
        writer.Write(header);
        writer.Write(payload);
    }

    private static byte[] ReadInnerPayload(byte[] fileBytes, ReadOnlySpan<byte> key32)
    {
        if (fileBytes.Length < HeaderSize)
        {
            throw new FormatException("APAK file is too small.");
        }

        var magic = BinaryFormatReader.ReadAscii(fileBytes.AsSpan(0, 4));
        if (magic != Magic)
        {
            throw new FormatException($"Invalid APAK magic: '{magic}'.");
        }

        var version = BinaryFormatReader.ReadUInt16LE(fileBytes.AsSpan(4));
        if (version != Version)
        {
            throw new FormatException($"Unsupported APAK version: {version}.");
        }

        var flags = BinaryFormatReader.ReadUInt32LE(fileBytes.AsSpan(8));
        var iv = fileBytes.AsSpan(12, RhythmAes256.IvSizeBytes).ToArray();
        var payloadLength = BinaryFormatReader.ReadUInt64LE(fileBytes.AsSpan(28));

        if (HeaderSize + payloadLength > (ulong)fileBytes.Length)
        {
            throw new FormatException("APAK payload length exceeds file size.");
        }

        var payload = fileBytes.AsSpan(HeaderSize, (int)payloadLength);

        if ((flags & FlagEncrypted) != 0)
        {
            return RhythmAes256.Decrypt(payload, key32, iv);
        }

        return payload.ToArray();
    }

    private static string SanitizeAssetName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Replace(' ', '_');
    }
}
