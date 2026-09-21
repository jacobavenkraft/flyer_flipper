using FlyerFlipper.UI.Dialogs;

namespace FlyerFlipper.Tests.TestSupport;

/// <summary>A folder picker that returns a canned answer and records what it was asked to start at.</summary>
internal sealed class StubFolderPicker : IFolderPicker
{
    /// <summary>What the next pick returns. Null means the user cancelled.</summary>
    public string? NextPickedFolder { get; set; }

    public int Calls { get; private set; }

    /// <summary>The <c>startIn</c> value of the most recent call.</summary>
    public string? LastStartIn { get; private set; }

    public Task<string?> PickFolderAsync(string? startIn)
    {
        Calls++;
        LastStartIn = startIn;
        return Task.FromResult(NextPickedFolder);
    }
}
