using Rythmbox.Core.Audio;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;
using Xunit;

namespace Rythmbox.Core.Tests;

public class StyleBankCodecTests
{
    [Fact]
    public void Load_valid_style_parses_patterns_and_macros()
    {
        var root = FindRepoRoot();
        var stylePath = Path.Combine(root, "Content", "Styles", "Pop", "pop_8beat_basic", "style.json");
        Assert.True(File.Exists(stylePath));

        var style = StyleBankCodec.Load(stylePath);

        Assert.Equal("pop_8beat_basic", style.Id);
        Assert.Equal("Pop 8 Beat", style.Name);
        Assert.Equal("Pop", style.Category);
        Assert.True(style.IsValid);
        Assert.Contains("intro_a", style.Patterns.Keys);
        Assert.Equal(0.45f, style.Macros.Complexity, precision: 2);
    }

    [Fact]
    public void Missing_midi_produces_warning_and_disabled_pattern()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rythmbox_style_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var json = """
            {
              "id": "test_style",
              "name": "Test",
              "category": "Pop",
              "patterns": {
                "verse_a": {
                  "name": "Verse A",
                  "type": "verse",
                  "midi": "missing.mid"
                }
              }
            }
            """;
            File.WriteAllText(Path.Combine(dir, "style.json"), json);

            var style = StyleBankCodec.Load(Path.Combine(dir, "style.json"));

            Assert.True(style.IsValid);
            Assert.NotEmpty(style.ValidationWarnings);
            Assert.False(style.Patterns["verse_a"].HasMidiFile);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Invalid_json_fails_gracefully_via_service()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rythmbox_style_bad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            File.WriteAllText(Path.Combine(dir, "style.json"), "{ not valid json");

            var service = new StyleBankService();
            service.Scan(dir);

            var style = Assert.Single(service.AllStyles);
            Assert.False(style.IsValid);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_required_fields_marks_style_invalid()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rythmbox_style_req_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            File.WriteAllText(Path.Combine(dir, "style.json"), """{"patterns": {}}""");

            var style = StyleBankCodec.Load(Path.Combine(dir, "style.json"));
            Assert.False(style.IsValid);
            Assert.NotEmpty(style.ValidationErrors);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            if (Directory.Exists(Path.Combine(dir, "Content", "Styles")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repo root with Content/Styles.");
    }
}

public class ArrangerSessionTests
{
    [Fact]
    public void Missing_pattern_shows_missing_pad_state()
    {
        var session = new ArrangerSession
        {
            SelectedStyle = new StyleDefinition
            {
                Id = "s",
                Name = "S",
                Category = "Pop",
                Patterns = new Dictionary<string, StylePattern>
                {
                    ["verse_a"] = new()
                    {
                        Id = "verse_a",
                        Name = "Verse A",
                        Type = PatternType.Verse,
                        ResolvedMidiPath = null,
                    },
                },
            },
        };

        var slot = PatternPadLayout.Slots.First(s => s.PatternKey == "verse_a");
        Assert.Equal(PatternPadVisualState.Missing, session.GetPadVisualState(slot));
    }

    [Fact]
    public void Queued_pattern_shows_queued_state()
    {
        var session = new ArrangerSession
        {
            SelectedStyle = MakeStyleWithMidi("verse_a"),
            SelectedPatternId = "intro_a",
            PlayingPatternId = "intro_a",
            QueuedPatternId = "verse_a",
        };

        var slot = PatternPadLayout.Slots.First(s => s.PatternKey == "verse_a");
        Assert.Equal(PatternPadVisualState.Queued, session.GetPadVisualState(slot));
    }

    private static StyleDefinition MakeStyleWithMidi(string patternId)
    {
        var path = Path.Combine(Path.GetTempPath(), "dummy.mid");
        File.WriteAllBytes(path, [0x4D, 0x54, 0x68, 0x64]);

        return new StyleDefinition
        {
            Id = "s",
            Name = "S",
            Category = "Pop",
            Patterns = new Dictionary<string, StylePattern>
            {
                [patternId] = new()
                {
                    Id = patternId,
                    Name = patternId,
                    Type = PatternType.Verse,
                    ResolvedMidiPath = path,
                },
            },
        };
    }
}

public class PlatformAudioBackendTests
{
    [Fact]
    public void Preferred_backend_matches_os()
    {
        var id = PlatformAudioBackend.PreferredBackendId;
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("WASAPI", id);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("ALSA", id);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("CoreAudio", id);
        }
    }
}
