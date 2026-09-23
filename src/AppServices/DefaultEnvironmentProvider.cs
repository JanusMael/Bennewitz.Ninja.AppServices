using System.Collections;
using Bennewitz.Ninja.AppServices.Abstractions;

namespace Bennewitz.Ninja.AppServices;

/// <summary>
/// Default <see cref="IEnvironmentProvider"/> that delegates directly to <see cref="System.Environment"/>.
/// </summary>
public sealed class DefaultEnvironmentProvider : IEnvironmentProvider
{
    /// <inheritdoc/>
    public IDictionary GetVariables(EnvironmentVariableTarget target)
    {
        return Environment.GetEnvironmentVariables(target);
    }

    /// <inheritdoc/>
    public void SetVariable(string name, string? value, EnvironmentVariableTarget target)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }
}