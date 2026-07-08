namespace Rythmbox.Core.Models;

/// <summary>A MIDI loop library folder (main RYTHM dir or a sub-bank).</summary>
public sealed record LoopBank(string Name, string Path);
