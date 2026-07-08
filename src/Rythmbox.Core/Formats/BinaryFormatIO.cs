using System.Buffers.Binary;
using System.Text;

namespace Rythmbox.Core.Formats;

internal static class BinaryFormatWriter
{
    public static void WriteAscii(Span<byte> dest, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > dest.Length)
        {
            throw new InvalidOperationException($"Value '{value}' exceeds field size {dest.Length}.");
        }

        bytes.CopyTo(dest);
    }

    public static void WriteUInt16LE(Span<byte> dest, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(dest, value);

    public static void WriteUInt32LE(Span<byte> dest, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(dest, value);

    public static void WriteUInt64LE(Span<byte> dest, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(dest, value);
}

internal static class BinaryFormatReader
{
    public static string ReadAscii(ReadOnlySpan<byte> span) => Encoding.ASCII.GetString(span).TrimEnd('\0');

    public static ushort ReadUInt16LE(ReadOnlySpan<byte> span) => BinaryPrimitives.ReadUInt16LittleEndian(span);

    public static uint ReadUInt32LE(ReadOnlySpan<byte> span) => BinaryPrimitives.ReadUInt32LittleEndian(span);

    public static ulong ReadUInt64LE(ReadOnlySpan<byte> span) => BinaryPrimitives.ReadUInt64LittleEndian(span);
}
