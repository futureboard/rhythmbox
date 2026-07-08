namespace Rythmbox.Core.Models.Styles;

public sealed class SongChainEntry
{
    public required string PatternId { get; init; }

    public uint RepeatCount { get; init; } = 1;
}
