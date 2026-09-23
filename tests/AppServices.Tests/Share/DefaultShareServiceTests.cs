using System.Diagnostics;
using Bennewitz.Ninja.AppServices;
using Bennewitz.Ninja.AppServices.Abstractions;

namespace AppServices.Tests.Share;

/// <summary>
/// <see cref="DefaultShareService"/> reports what it actually did. Every launch goes through an
/// injected launcher, so no test starts a browser, a file manager or a mail client.
/// </summary>
/// <remarks>
/// <para>
/// Ported from ClaudeForge's <c>ShareOutcomeTests</c> and <c>ShareServiceTests</c>, which
/// exercised this class from the consumer side through its public API.
/// </para>
/// <para>
/// ⛔ <b>The assertions are on the returned OUTCOME.</b> While <c>ShareTextAsync</c> returned a
/// bare task, "the browser opened" and "no branch matched and nothing happened" were the same
/// observation, and Windows text-sharing was the second of those in every shipped build. A test
/// asserting only that nothing threw is what let it survive.
/// </para>
/// <para>
/// ⚠ <b>No test here passes TEXT without a URI on Windows or macOS.</b> That path runs the real
/// <c>clip.exe</c> or <c>pbcopy</c>, not the injected launcher, and would overwrite the
/// developer's clipboard. The original <c>ShareTextAsync_DoesNotThrow</c> did exactly that on
/// Windows and was not ported; the empty-payload test covers its no-throw claim without touching
/// the clipboard.
/// </para>
/// </remarks>
public sealed class DefaultShareServiceTests
{
    /// <summary>A launcher that reports success without starting anything.</summary>
    private static Process? Launched(ProcessStartInfo _) => new();

    /// <summary>A launcher that reports the process did not start, as <c>Process.Start</c> does.</summary>
    private static Process? NotLaunched(ProcessStartInfo _) => null;

    // -----------------------------------------------------------------------
    // Text and URIs
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShareText_WithNothingToShare_IsUnavailableRatherThanSuccess()
    {
        DefaultShareService svc = new(NotLaunched);

        ShareOutcome outcome = await svc.ShareTextAsync("title", string.Empty, uri: null, CancellationToken.None);

        // An empty payload reaches no platform branch. Anything else would be the original
        // defect restated: claiming an action that did not happen.
        Assert.Equal(ShareOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task ShareText_WithUri_WhenLaunchSucceeds_IsOpenedInBrowser()
    {
        DefaultShareService svc = new(Launched);

        ShareOutcome outcome = await svc.ShareTextAsync("title", "body", "https://example.invalid/", CancellationToken.None);

        // Every platform hands a caller-supplied URI to the default browser.
        Assert.Equal(ShareOutcome.OpenedInBrowser, outcome);
    }

    [Fact]
    public async Task ShareText_WithUri_WhenLaunchFails_IsFailed()
    {
        DefaultShareService svc = new(NotLaunched);

        ShareOutcome outcome = await svc.ShareTextAsync("title", "body", "https://example.invalid/", CancellationToken.None);

        // ⛔ The assertion the whole outcome type exists for. TryStart used to return void and
        // swallow the failure, which is why a broken share looked identical to a working one.
        Assert.Equal(ShareOutcome.Failed, outcome);
    }

    /// <summary>
    /// The caller's URI is what reaches the launcher, and it reaches it once.
    /// </summary>
    /// <remarks>
    /// ⓘ Tightened in the port. The original asserted "0 or 1 launches depending on OS", but every
    /// supported platform hands a URI over in exactly one launch — the outcome tests above already
    /// depend on that — so 0 would be a defect this test used to accept.
    /// </remarks>
    [Fact]
    public async Task ShareText_WithUri_HandsThatUriToOneLaunch()
    {
        const string uri = "https://example.invalid/shared";
        List<ProcessStartInfo> launches = [];
        DefaultShareService svc = new(psi =>
        {
            launches.Add(psi);
            return new Process();
        });

        await svc.ShareTextAsync("title", "body", uri, CancellationToken.None);

        ProcessStartInfo launch = Assert.Single(launches);
        Assert.True(launch.FileName == uri || launch.ArgumentList.Contains(uri),
            $"The launch was '{launch.FileName}' with arguments [{string.Join(", ", launch.ArgumentList)}], " +
            $"and nowhere in it is the URI the caller asked to share, '{uri}'.");
    }

    /// <summary>
    /// With text and no URI, Linux hands the desktop a <c>mailto:</c> carrying both.
    /// </summary>
    /// <remarks>
    /// ⓘ The original suite left this branch uncovered, because it is unreachable on any other
    /// host and a test that quietly asserted nothing there would read as coverage. A dynamic skip
    /// reports it as SKIPPED instead, so it runs where it can, which includes CI, and claims
    /// nothing where it cannot.
    /// </remarks>
    [Fact]
    public async Task ShareText_WithTextAndNoUri_OnLinux_OpensTheMailClient()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The mailto: branch exists only on Linux.");

        List<ProcessStartInfo> launches = [];
        DefaultShareService svc = new(psi =>
        {
            launches.Add(psi);
            return new Process();
        });

        ShareOutcome outcome = await svc.ShareTextAsync("A title", "the body", uri: null, CancellationToken.None);

        Assert.Equal(ShareOutcome.OpenedMailClient, outcome);
        ProcessStartInfo launch = Assert.Single(launches);
        Assert.Equal("mailto:?subject=A%20title&body=the%20body", launch.FileName);
    }

    // -----------------------------------------------------------------------
    // Files
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShareFile_WhenPathIsNotOnDisk_IsUnavailableNotFailed()
    {
        DefaultShareService svc = new(Launched);
        string missing = Path.Combine(Path.GetTempPath(), $"share-outcome-{Guid.NewGuid():N}.txt");

        ShareOutcome outcome = await svc.ShareFileAsync("title", missing, CancellationToken.None);

        // Nothing was attempted, so this is not a failure. The two stay distinct because a host's
        // status pill keeps a failure on screen until dismissed and clears a non-failure.
        Assert.Equal(ShareOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task ShareFile_WhenLaunchFails_IsFailed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-outcome-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "payload");
        try
        {
            DefaultShareService svc = new(NotLaunched);

            ShareOutcome outcome = await svc.ShareFileAsync("title", path, CancellationToken.None);

            // Premise first: the file really is on disk, so this exercises the launch arm and not
            // the missing-path arm above, which returns Unavailable for a different reason.
            Assert.True(File.Exists(path), "Setup failed: the file under test must exist.");

            // An existing file whose file manager would not start is a failure, not an absence.
            Assert.Equal(ShareOutcome.Failed, outcome);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ShareFile_WhenLaunchSucceeds_IsRevealedInFileManager()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-outcome-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "payload");
        try
        {
            DefaultShareService svc = new(Launched);

            ShareOutcome outcome = await svc.ShareFileAsync("title", path, CancellationToken.None);

            // All three platforms reveal the file rather than opening a share sheet, and the
            // outcome must say so rather than claim a share that is not available anywhere.
            Assert.Equal(ShareOutcome.RevealedInFileManager, outcome);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -----------------------------------------------------------------------
    // Cancellation -- new with required tokens, and covered nowhere before
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ShareText_WhenAlreadyCancelled_IsCancelled_AndLaunchesNothing()
    {
        int launches = 0;
        DefaultShareService svc = new(_ =>
        {
            launches++;
            return new Process();
        });

        // A URI, so that without the guard this call WOULD launch on every platform.
        ShareOutcome outcome = await svc.ShareTextAsync("title", "body", "https://example.invalid/",
            new CancellationToken(canceled: true));

        // ⚠ Cancelled, not Failed. Nothing went wrong, so a host must not report that something did.
        Assert.Equal(ShareOutcome.Cancelled, outcome);
        Assert.Equal(0, launches);
    }

    [Fact]
    public async Task ShareFile_WhenAlreadyCancelled_IsCancelled_AndLaunchesNothing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-outcome-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "payload");
        try
        {
            int launches = 0;
            DefaultShareService svc = new(_ =>
            {
                launches++;
                return new Process();
            });

            // The file exists, so that without the guard this call WOULD launch on every platform.
            ShareOutcome outcome = await svc.ShareFileAsync("title", path, new CancellationToken(canceled: true));

            Assert.Equal(ShareOutcome.Cancelled, outcome);
            Assert.Equal(0, launches);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -----------------------------------------------------------------------
    // The type itself
    // -----------------------------------------------------------------------

    /// <remarks>
    /// ⓘ Read through <see cref="Enum.GetName{TEnum}(TEnum)"/> rather than as a cast to int, which
    /// is a constant the compiler folds. The pinned decision is real either way.
    /// </remarks>
    [Fact]
    public void TheDefaultOutcome_IsFailed_SoAnUnsetValueIsLoud()
    {
        // ⚠ Deliberate. A fake or a half-written implementation returning default(ShareOutcome)
        // must land on a pill that sticks, not on a success that clears itself after six seconds.
        // Renumbering the enum puts the honest direction on the quiet side.
        Assert.Equal(nameof(ShareOutcome.Failed), Enum.GetName(default(ShareOutcome)));
    }

    /// <summary>
    /// Constructing with no arguments — what a host's composition root does — starts nothing.
    /// </summary>
    [Fact]
    public void ConstructsWithNoArguments_AndLaunchesNothing()
    {
        bool launched = false;
        DefaultShareService svc = new(_ =>
        {
            launched = true;
            return null;
        });

        Assert.NotNull(svc);
        Assert.False(launched, "Construction must not start a process.");

        // The parameterless form is the one a host uses; it must not throw either.
        Assert.NotNull(new DefaultShareService());
    }
}
