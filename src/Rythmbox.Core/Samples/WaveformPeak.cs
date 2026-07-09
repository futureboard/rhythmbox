namespace Rythmbox.Core.Samples;

/// <summary>Min/max sample values for one waveform column (NAudio MaxPeakProvider / BBC audiowaveform).</summary>
public readonly record struct WaveformPeak(float Min, float Max);
