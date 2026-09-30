using System.Runtime.CompilerServices;

// Lets the test project reach the one seam a test cannot reach from outside: pressing Ctrl+C in a
// console run (EntryRun.Current.PressCtrlC). ConsoleCancelEventArgs has no public constructor, so
// without this the 130 exit could only be seen from a child process, and never on Windows.
[assembly: InternalsVisibleTo("AppServices.Tests")]
