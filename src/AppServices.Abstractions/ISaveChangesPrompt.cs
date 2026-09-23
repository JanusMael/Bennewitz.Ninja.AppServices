namespace Bennewitz.Ninja.AppServices.Abstractions;

/// <summary>
/// Marker interface for objects that can be shown in a "save pending changes?"
/// confirmation dialog via <see cref="IDialogService.ShowSaveChangesDialogAsync"/>.
/// </summary>
/// <remarks>
/// The concrete implementation (e.g. <c>SaveChangesDialogViewModel</c>) lives in the
/// consuming application and carries the domain-specific diff data.
/// <c>AvaloniaDialogService</c> receives a factory delegate at startup that
/// knows the concrete type and how to present it — keeping the service layer
/// independent of any application ViewModel.
/// </remarks>
public interface ISaveChangesPrompt
{
}