using Bennewitz.Ninja.AppServices.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// Tests for <see cref="NativeErrorDialog.ShowFatalError"/>.
/// <para>
/// The primary contract is that the method never throws, even when the platform
/// helpers (<c>zenity</c>, <c>osascript</c>, Win32 MessageBox) are unavailable.
/// The internal <c>SuppressForTests</c> flag (exposed via
/// <c>InternalsVisibleTo</c>) is set to <c>true</c> so that no real OS dialog is
/// shown during the test run.
/// </para>
/// </summary>
public sealed class NativeErrorDialogTests : IDisposable
{
    // Ported from MSTest's [TestInitialize]: xunit v3 builds a fresh instance per test, so the
    // constructor runs before each test exactly as TestInitialize did.
    public NativeErrorDialogTests()
    {
        NativeErrorDialog.SuppressForTests = true;
    }

    // Ported from MSTest's [TestCleanup].
    public void Dispose()
    {
        NativeErrorDialog.Reset();
    }

    // -----------------------------------------------------------------------
    // Never-throws contract
    // -----------------------------------------------------------------------

    [Fact]
    public void ShowFatalError_DoesNotThrow_WithNormalInput()
    {
        NativeErrorDialog.ShowFatalError("Test Title", "Test message.");
    }

    [Fact]
    public void ShowFatalError_DoesNotThrow_WithEmptyStrings()
    {
        NativeErrorDialog.ShowFatalError(string.Empty, string.Empty);
    }

    [Fact]
    public void ShowFatalError_DoesNotThrow_WithExceptionToString()
    {
        InvalidOperationException ex = new("something went wrong",
            new ArgumentNullException("inner"));
        NativeErrorDialog.ShowFatalError("Sample App — Fatal Error", ex.ToString());
    }

    [Fact]
    public void ShowFatalError_DoesNotThrow_WithSpecialCharacters()
    {
        NativeErrorDialog.ShowFatalError(
            "\"Error\" with special chars: \\ / ' \"",
            "Line1\nLine2\r\nLine3\t<tab>");
    }

    // -----------------------------------------------------------------------
    // Argument recording (via the suppression seam)
    // -----------------------------------------------------------------------

    [Fact]
    public void ShowFatalError_RecordsTitle_WhenSuppressed()
    {
        NativeErrorDialog.ShowFatalError("My Title", "My Message");
        Assert.Equal("My Title", NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void ShowFatalError_RecordsMessage_WhenSuppressed()
    {
        NativeErrorDialog.ShowFatalError("Title", "Detailed message text");
        Assert.Equal("Detailed message text", NativeErrorDialog.LastSuppressedCall.Message);
    }

    [Fact]
    public void Reset_ClearsSuppressedCallRecord()
    {
        NativeErrorDialog.ShowFatalError("A", "B");
        NativeErrorDialog.Reset();

        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Message);
        NativeErrorDialog.SuppressForTests = true;
    }
}