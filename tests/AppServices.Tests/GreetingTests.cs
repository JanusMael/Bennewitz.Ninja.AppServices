using Bennewitz.Ninja.AppServices;

namespace AppServices.Tests;

/// <summary>
/// A real passing test from the first run, so a red build after generating means you broke
/// something rather than that setup is unfinished.
/// </summary>
public sealed class GreetingTests
{
    [Fact]
    public void A_name_is_greeted_by_name() =>
        Assert.Equal("Hello, Ada.", Greeting.For("Ada"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Absent_and_whitespace_only_names_fall_back(string? name) =>
        Assert.Equal("Hello.", Greeting.For(name));

    [Fact]
    public void Surrounding_whitespace_is_trimmed() =>
        Assert.Equal("Hello, Ada.", Greeting.For("  Ada  "));
}
