// Tests in this assembly run one at a time.
//
// ⛔ Ported from the original suite's [assembly: DoNotParallelize], and still required: tests here
// change process-wide STATIC state (AvaloniaDiagnostics' configure-once bootstrap, Serilog's
// Log.Logger, NativeErrorDialog's test hooks, Console's streams) and put it back in Dispose, and
// AppMain's in-process runs register process-wide handlers (unhandled and unobserved exceptions,
// ProcessExit, Ctrl+C, SIGTERM) until they end. xunit v3 parallelises across classes by default,
// so without this two of them would race.
//
// The headless Avalonia tests add a second reason: one session and one dispatcher, which must
// not be driven by two tests at once. Headless/HeadlessSessionTests.cs guards that premise.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
