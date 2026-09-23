using System.Runtime.CompilerServices;

// Lets the test project reach ShellLauncher's internal helpers (BuildWindowsTerminalPsi,
// BuildDirectPowerShellPsi and the Probe* wrappers) and NativeErrorDialog's test hooks.
//
// ⚠ This grant existed before the extraction, naming ClaudeForge.Tests, in an AssemblyInfo.cs that
// did not travel with the source. The tests that needed it could not compile against the package
// until it was restored under this repository's own test assembly name. A wrong or missing name
// fails silently here: the grant simply goes nowhere, and only the test that needs it notices.
[assembly: InternalsVisibleTo("AppServices.Tests")]
