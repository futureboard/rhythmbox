using Rythmbox.Core.Engine;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;
using Xunit;

namespace Rythmbox.Core.Tests;

public class TimeSignatureTests
{
    [Theory]
    [InlineData("4/4", 4, 4)]
    [InlineData("2/4", 2, 4)]
    [InlineData(" 6 / 8 ", 6, 8)]
    [InlineData("3/4", 3, 4)]
    public void Parse_reads_valid_signatures(string text, int num, int den)
    {
        Assert.True(TimeSignature.TryParse(text, out var sig));
        Assert.Equal(num, sig.Numerator);
        Assert.Equal(den, sig.Denominator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4")]
    [InlineData("4/3")]
    [InlineData("0/4")]
    [InlineData("x/y")]
    public void Parse_falls_back_to_four_four_when_invalid(string? text)
    {
        Assert.False(TimeSignature.TryParse(text, out _));
        Assert.Equal(TimeSignature.FourFour, TimeSignature.Parse(text));
    }

    [Fact]
    public void QuarterNotesPerBar_reflects_denominator()
    {
        Assert.Equal(4.0, new TimeSignature(4, 4).QuarterNotesPerBar, precision: 3);
        Assert.Equal(2.0, new TimeSignature(2, 4).QuarterNotesPerBar, precision: 3);
        Assert.Equal(3.0, new TimeSignature(6, 8).QuarterNotesPerBar, precision: 3);
    }
}

public class TimeSignatureControllerTests
{
    [Fact]
    public void Momentary_switch_lasts_one_bar_then_reverts_to_base()
    {
        var controller = new TimeSignatureController();
        controller.SetBase(TimeSignature.FourFour);

        controller.TriggerMomentary(new TimeSignature(2, 4));
        Assert.True(controller.HasPendingOverride);
        Assert.Equal(TimeSignature.FourFour, controller.Current); // not applied until the next bar

        Assert.Equal(new TimeSignature(2, 4), controller.AdvanceBar());
        Assert.False(controller.HasPendingOverride);

        Assert.Equal(TimeSignature.FourFour, controller.AdvanceBar());
        Assert.Equal(TimeSignature.FourFour, controller.AdvanceBar());
    }

    [Fact]
    public void Momentary_switch_can_span_multiple_bars()
    {
        var controller = new TimeSignatureController();
        controller.TriggerMomentary(new TimeSignature(3, 4), bars: 2);

        Assert.Equal(new TimeSignature(3, 4), controller.AdvanceBar());
        Assert.Equal(new TimeSignature(3, 4), controller.AdvanceBar());
        Assert.Equal(TimeSignature.FourFour, controller.AdvanceBar());
    }

    [Fact]
    public void ClearOverride_returns_to_base_on_next_bar()
    {
        var controller = new TimeSignatureController();
        controller.TriggerMomentary(new TimeSignature(2, 4), bars: 4);
        controller.ClearOverride();

        Assert.False(controller.HasPendingOverride);
        Assert.Equal(TimeSignature.FourFour, controller.AdvanceBar());
    }

    [Fact]
    public void SetBase_applies_immediately_only_without_active_override()
    {
        var controller = new TimeSignatureController();

        controller.SetBase(new TimeSignature(3, 4));
        Assert.Equal(new TimeSignature(3, 4), controller.Current);

        controller.TriggerMomentary(new TimeSignature(2, 4));
        controller.SetBase(new TimeSignature(6, 8));
        Assert.Equal(new TimeSignature(3, 4), controller.Current); // masked by pending override

        controller.AdvanceBar(); // 2/4 momentary bar
        Assert.Equal(new TimeSignature(6, 8), controller.AdvanceBar()); // reverts to new base
    }
}

public class MidiFootSwitchControllerTests
{
    [Fact]
    public void Press_fires_once_per_rising_edge()
    {
        var controller = new MidiFootSwitchController(controllerNumber: 64);
        var presses = 0;
        controller.Pressed += () => presses++;

        Assert.True(controller.ProcessControlChange(64, 127));  // down -> press
        Assert.False(controller.ProcessControlChange(64, 100)); // still down, no new edge
        Assert.False(controller.ProcessControlChange(64, 0));   // release
        Assert.True(controller.ProcessControlChange(64, 127));  // down again -> press

        Assert.Equal(2, presses);
    }

    [Fact]
    public void Ignores_other_controllers_and_when_disabled()
    {
        var controller = new MidiFootSwitchController(controllerNumber: 64);
        var presses = 0;
        controller.Pressed += () => presses++;

        Assert.False(controller.ProcessControlChange(1, 127)); // modulation wheel, not our CC
        Assert.Equal(0, presses);

        controller.IsEnabled = false;
        Assert.False(controller.ProcessControlChange(64, 127));
        Assert.Equal(0, presses);
    }
}
