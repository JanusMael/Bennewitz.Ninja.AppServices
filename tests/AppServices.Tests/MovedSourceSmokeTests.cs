using System.Collections;
using Bennewitz.Ninja.AppServices;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;

namespace AppServices.Tests;

/// <summary>
/// Exercises the types that moved out of OpenForge2k, so the split is proven by running the code
/// rather than by the fact that it compiled.
/// </summary>
/// <remarks>
/// ⚠ These are smoke tests, not the layering guards. Those are step 5 of plan 00002 and must scan
/// csproj XML as well as reflection: the compiler omits an unused reference from the assembly
/// reference table, so a declared-but-unused bad reference is invisible to reflection alone.
/// </remarks>
public sealed class MovedSourceSmokeTests
{
    [Fact]
    public void Environment_provider_round_trips_a_process_scoped_variable()
    {
        DefaultEnvironmentProvider provider = new();
        string name = "BB_APPSERVICES_TEST_" + Guid.NewGuid().ToString("N");

        try
        {
            provider.SetVariable(name, "set-by-test", EnvironmentVariableTarget.Process);

            IDictionary variables = provider.GetVariables(EnvironmentVariableTarget.Process);

            Assert.True(variables.Contains(name));
            Assert.Equal("set-by-test", variables[name]);
        }
        finally
        {
            provider.SetVariable(name, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void Environment_provider_satisfies_the_abstraction_it_moved_beside()
    {
        // The contract and its default implementation now live in different packages. That this
        // still binds is the whole point of the split, so assert it rather than assume it.
        IEnvironmentProvider provider = new DefaultEnvironmentProvider();

        Assert.NotNull(provider.GetVariables(EnvironmentVariableTarget.Process));
    }

    [Fact]
    public void Plain_dialog_message_carries_one_text_segment()
    {
        DialogMessage message = DialogMessage.Plain("something went wrong");

        DialogSegment only = Assert.Single(message.Segments);
        Assert.Equal(DialogSegmentKind.Text, only.Kind);
        Assert.Equal("something went wrong", only.Value);
    }

    [Fact]
    public void Dialog_message_rejects_null_segments()
    {
        Assert.Throws<ArgumentNullException>(() => new DialogMessage(null!));
    }
}
