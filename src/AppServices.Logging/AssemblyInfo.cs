using System.Runtime.CompilerServices;

// Lets the test project reach BucketedRollingFileSink's internal seams: BucketStart,
// TryParseBucketStamp and StartupPruneTask, which the bucket arithmetic and retention tests pin.
//
// ⚠ Before the extraction this came from a solution-wide file that granted every assembly's
// internals to every sibling, product assemblies included, and it did not travel with the source.
// Restored NARROWLY: one grant, to this repository's own test assembly, and only because a test
// needs it.
[assembly: InternalsVisibleTo("AppServices.Tests")]
