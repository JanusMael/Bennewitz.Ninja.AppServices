using System.Runtime.CompilerServices;

// Lets the test project reach SerilogAvaloniaSink.Map, the level mapping between Avalonia's logger
// and Serilog that the sink tests pin case by case.
//
// ⚠ Before the extraction this came from a solution-wide file that granted every assembly's
// internals to every sibling, product assemblies included, and it did not travel with the source.
// Restored NARROWLY: one grant, to this repository's own test assembly, and only because a test
// needs it.
[assembly: InternalsVisibleTo("AppServices.Tests")]
