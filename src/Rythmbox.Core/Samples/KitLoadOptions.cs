namespace Rythmbox.Core.Samples;

/// <summary>Boundaries applied while opening a JSON kit to avoid an untrusted preset exhausting process memory or handles.</summary>
public sealed class KitLoadOptions
{
    /// <summary>Maximum number of unique WAV files a preset may map.</summary>
    public int MaxMappedSampleCount { get; init; } = 1_024;

    /// <summary>Largest individual WAV file allowed in a mapped kit.</summary>
    public long MaxSingleMappedSampleBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Aggregate size of unique WAV files allowed in a mapped kit.</summary>
    public long MaxTotalMappedSampleBytes { get; init; } = 2L * 1024 * 1024 * 1024;
}

/// <summary>Background loading status used by the UI loading dialog.</summary>
public sealed record KitLoadProgress(int CompletedSamples, int TotalSamples, string Message, long MappedBytes, int SkippedSamples)
{
    public double Fraction => TotalSamples <= 0 ? 0d : Math.Clamp((double)CompletedSamples / TotalSamples, 0d, 1d);
}

/// <summary>Kit plus non-fatal validation messages emitted while applying load limits.</summary>
public sealed record KitLoadResult(Models.KitPreset Kit, IReadOnlyList<string> Warnings, long MappedBytes, int MappedSampleCount);
