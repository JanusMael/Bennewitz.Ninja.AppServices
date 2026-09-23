// Tests in this assembly run one at a time.
//
// ⛔ Ported from the original suite's [assembly: DoNotParallelize], and required for the same
// reason: SerilogAvaloniaSinkTests and NativeErrorDialogTests change process-wide STATIC state
// (Avalonia's logger sink, the dialog's test hooks). MSTest ran that suite serially; xunit v3
// parallelises across classes by default, so without this the port would introduce races the
// originals never had.
//
// The headless Avalonia tests add a second reason: one session and one dispatcher, which must
// not be driven by two tests at once. See Headless/HeadlessSessionBootstrap.cs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
