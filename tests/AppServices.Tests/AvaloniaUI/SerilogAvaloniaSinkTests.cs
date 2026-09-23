using Bennewitz.Ninja.AppServices.AvaloniaUI;
using AvaloniaLevel = Avalonia.Logging.LogEventLevel;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace AppServices.Tests.AvaloniaUI;

public sealed class SerilogAvaloniaSinkTests
{
    // -----------------------------------------------------------------------
    // Level mapping
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AvaloniaLevel.Verbose, SerilogLevel.Verbose)]
    [InlineData(AvaloniaLevel.Debug, SerilogLevel.Debug)]
    [InlineData(AvaloniaLevel.Information, SerilogLevel.Information)]
    [InlineData(AvaloniaLevel.Warning, SerilogLevel.Warning)]
    [InlineData(AvaloniaLevel.Error, SerilogLevel.Error)]
    [InlineData(AvaloniaLevel.Fatal, SerilogLevel.Fatal)]
    public void Map_ConvertsEveryLevel_Correctly(AvaloniaLevel avalonia, SerilogLevel expected)
    {
        Assert.Equal(expected, SerilogAvaloniaSink.Map(avalonia));
    }

    [Fact]
    public void Map_UnknownLevel_ReturnInformation()
    {
        AvaloniaLevel unknown = (AvaloniaLevel)999;
        Assert.Equal(SerilogLevel.Information, SerilogAvaloniaSink.Map(unknown));
    }

    // -----------------------------------------------------------------------
    // IsEnabled
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AvaloniaLevel.Verbose)]
    [InlineData(AvaloniaLevel.Debug)]
    [InlineData(AvaloniaLevel.Information)]
    [InlineData(AvaloniaLevel.Warning)]
    [InlineData(AvaloniaLevel.Error)]
    [InlineData(AvaloniaLevel.Fatal)]
    public void IsEnabled_True_ForUnmutedArea_AtAnyLevel(AvaloniaLevel level)
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(level, "Binding"));
    }

    [Fact]
    public void IsEnabled_True_ForUnmutedAreas_BelowWarning()
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, string.Empty));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Binding"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Information, "Binding"));
    }

    [Theory]
    [InlineData("Layout")]
    [InlineData("Property")]
    [InlineData("Visual")]
    public void IsEnabled_False_ForMutedAreas_BelowWarning(string area)
    {
        SerilogAvaloniaSink sink = new();
        Assert.False(sink.IsEnabled(AvaloniaLevel.Verbose, area));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Debug, area));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Information, area));
    }

    [Theory]
    [InlineData("Layout")]
    [InlineData("Property")]
    [InlineData("Visual")]
    public void IsEnabled_True_ForMutedAreas_AtWarningAndAbove(string area)
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(AvaloniaLevel.Warning, area));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Error, area));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Fatal, area));
    }

    [Fact]
    public void IsEnabled_CustomMutedAreas_OverrideDefault()
    {
        // Layout is no longer muted — only "MyNoisyArea" is.
        SerilogAvaloniaSink sink = new(["MyNoisyArea"]);

        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Layout"));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Debug, "MyNoisyArea"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Warning, "MyNoisyArea"));
    }

    [Fact]
    public void IsEnabled_EmptyMutedCollection_DisablesMutingEntirely()
    {
        SerilogAvaloniaSink sink = new([]);

        // Every default-muted area is now un-muted.
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Layout"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Property"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Visual"));
    }
}