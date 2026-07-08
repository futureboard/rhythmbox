using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.Core.Formats;

/// <summary>
/// RhythmLive packed style container (<c>.rhmsty</c>).
/// Binary layout v1:
///   Header (32 bytes)
///   Manifest UTF-8 JSON (same schema as <c>style.json</c>; pattern <c>midi</c> uses <c>#N</c> blob refs)
///   Blob table (count × 16 bytes): offset u64, length u32, reserved u32
///   Raw Standard MIDI File bytes for each pattern
/// </summary>
public static class RhmStyleCodec
{
    public const string Extension = ".rhmsty";
    public const string Magic = "RHMSTY";
    public const ushort Version = 1;

    private const int HeaderSize = 32;
    private const int BlobEntrySize = 16;

    public sealed class PackResult
    {
        public required string OutputPath { get; init; }

        public int BlobCount { get; init; }
    }

    public static StyleDefinition Load(string rhmstyPath)
    {
        var bytes = File.ReadAllBytes(rhmstyPath);
        var (manifestBytes, blobData) = Parse(bytes);

        var styleId = JsonDocument.Parse(manifestBytes).RootElement.TryGetProperty("id", out var idEl)
            ? idEl.GetString() ?? Path.GetFileNameWithoutExtension(rhmstyPath)
            : Path.GetFileNameWithoutExtension(rhmstyPath);

        var styleDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RhythmLive",
            "styles",
            styleId);

        if (Directory.Exists(styleDir))
        {
            Directory.Delete(styleDir, recursive: true);
        }

        Directory.CreateDirectory(styleDir);
        return ExtractToDirectory(manifestBytes, blobData, styleDir);
    }

    public static PackResult PackDirectory(string styleDirectory, string outputPath)
    {
        var styleJsonPath = Path.Combine(styleDirectory, "style.json");
        if (!File.Exists(styleJsonPath))
        {
            throw new FileNotFoundException("style.json not found in style directory.", styleJsonPath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(styleJsonPath));
        var root = doc.RootElement.Clone();

        if (!root.TryGetProperty("patterns", out var patternsEl) || patternsEl.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("style.json is missing a patterns object.");
        }

        var blobs = new List<byte[]>();
        var manifest = JsonNode.Parse(root.GetRawText())!.AsObject();
        var manifestPatterns = manifest["patterns"]!.AsObject();

        foreach (var property in patternsEl.EnumerateObject())
        {
            var patternId = property.Name;
            var midiRel = property.Value.TryGetProperty("midi", out var midiEl)
                ? midiEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(midiRel) || midiRel.StartsWith('#'))
            {
                continue;
            }

            var midiPath = ResolveMidiPath(styleDirectory, midiRel);
            if (midiPath is null || !File.Exists(midiPath))
            {
                throw new FileNotFoundException($"Pattern '{patternId}' MIDI not found: {midiRel}", midiPath ?? midiRel);
            }

            var blobIndex = blobs.Count;
            blobs.Add(File.ReadAllBytes(midiPath));
            manifestPatterns[patternId]!["midi"] = $"#{blobIndex}";
        }

        var manifestBytes = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        WriteFile(outputPath, manifestBytes, blobs);
        return new PackResult { OutputPath = outputPath, BlobCount = blobs.Count };
    }

    private static StyleDefinition ExtractToDirectory(byte[] manifestBytes, IReadOnlyList<byte[]> blobData, string styleDir)
    {
        var manifest = JsonNode.Parse(manifestBytes)!.AsObject();
        if (manifest["patterns"] is JsonObject patterns)
        {
            foreach (var (patternId, node) in patterns.ToList())
            {
                if (node is not JsonObject patternObj)
                {
                    continue;
                }

                var midiRef = patternObj["midi"]?.GetValue<string>();
                if (midiRef is null || !midiRef.StartsWith('#'))
                {
                    continue;
                }

                if (!int.TryParse(midiRef.AsSpan(1), out var blobIndex)
                    || blobIndex < 0
                    || blobIndex >= blobData.Count)
                {
                    throw new FormatException($"Pattern '{patternId}' has invalid blob ref '{midiRef}'.");
                }

                var fileName = $"{patternId}.mid";
                File.WriteAllBytes(Path.Combine(styleDir, fileName), blobData[blobIndex]);
                patternObj["midi"] = fileName;
            }
        }

        var styleJsonPath = Path.Combine(styleDir, "style.json");
        File.WriteAllText(styleJsonPath, manifest.ToJsonString());
        return StyleBankCodec.Load(styleJsonPath);
    }

    private static void WriteFile(string outputPath, byte[] manifestBytes, IReadOnlyList<byte[]> blobs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        var header = new byte[HeaderSize];
        BinaryFormatWriter.WriteAscii(header, Magic);
        BinaryFormatWriter.WriteUInt16LE(header.AsSpan(6), Version);
        BinaryFormatWriter.WriteUInt32LE(header.AsSpan(8), 0);
        BinaryFormatWriter.WriteUInt32LE(header.AsSpan(12), (uint)manifestBytes.Length);
        BinaryFormatWriter.WriteUInt32LE(header.AsSpan(16), (uint)blobs.Count);
        writer.Write(header);
        writer.Write(manifestBytes);

        var cursor = stream.Position + blobs.Count * BlobEntrySize;
        foreach (var blob in blobs)
        {
            var entry = new byte[BlobEntrySize];
            BinaryFormatWriter.WriteUInt64LE(entry.AsSpan(0), (ulong)cursor);
            BinaryFormatWriter.WriteUInt32LE(entry.AsSpan(8), (uint)blob.Length);
            writer.Write(entry);
            cursor += blob.Length;
        }

        foreach (var blob in blobs)
        {
            writer.Write(blob);
        }
    }

    private static (byte[] Manifest, List<byte[]> Blobs) Parse(byte[] fileBytes)
    {
        if (fileBytes.Length < HeaderSize)
        {
            throw new FormatException("RHMSTY file is too small.");
        }

        var magic = BinaryFormatReader.ReadAscii(fileBytes.AsSpan(0, 6));
        if (magic != Magic)
        {
            throw new FormatException($"Invalid RHMSTY magic: '{magic}'.");
        }

        var version = BinaryFormatReader.ReadUInt16LE(fileBytes.AsSpan(6));
        if (version != Version)
        {
            throw new FormatException($"Unsupported RHMSTY version: {version}.");
        }

        var manifestLength = BinaryFormatReader.ReadUInt32LE(fileBytes.AsSpan(12));
        var blobCount = BinaryFormatReader.ReadUInt32LE(fileBytes.AsSpan(16));

        var manifestOffset = HeaderSize;
        var tableOffset = manifestOffset + manifestLength;
        var dataOffset = tableOffset + blobCount * BlobEntrySize;

        if (dataOffset > fileBytes.Length)
        {
            throw new FormatException("RHMSTY header/table extends past end of file.");
        }

        var manifestBytes = fileBytes.AsSpan(manifestOffset, (int)manifestLength).ToArray();
        var blobs = new List<byte[]>();
        var span = fileBytes.AsSpan();

        for (var i = 0; i < blobCount; i++)
        {
            var entryOffset = (int)(tableOffset + i * BlobEntrySize);
            var offset = BinaryFormatReader.ReadUInt64LE(span.Slice(entryOffset, 8));
            var length = BinaryFormatReader.ReadUInt32LE(span.Slice(entryOffset + 8, 4));

            if (offset + length > (ulong)fileBytes.Length)
            {
                throw new FormatException($"Blob {i} extends past end of file.");
            }

            blobs.Add(fileBytes.AsSpan((int)offset, (int)length).ToArray());
        }

        return (manifestBytes, blobs);
    }

    private static string? ResolveMidiPath(string styleDir, string relative) =>
        new[]
        {
            Path.GetFullPath(Path.Combine(styleDir, relative)),
            Path.GetFullPath(relative),
        }.FirstOrDefault(File.Exists);
}
