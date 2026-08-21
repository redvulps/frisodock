using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Single responsibility: moving a pinned app to another place in the list. A pure function, no UI.
///
/// The pinned ones always occupy the dock's first positions, and in the same order as the list — so the
/// index in the dock and the index here are the same number, and nothing has to be translated.
/// </summary>
public sealed class PinnedAppsReorder
{
    /// <summary>
    /// A new list with the app at the requested position. It returns the original list, without copying,
    /// when there is nothing to do: the app is not pinned, or it is already where it should be.
    /// </summary>
    /// <param name="targetIndex">
    /// Destination, clamped to the pinned range. Dragging a pinned app past the last of them
    /// stops at the end of the block, instead of mixing it with the running apps.
    /// </param>
    public IReadOnlyList<PinnedApp> Move(IReadOnlyList<PinnedApp> pinned, AppKey key, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(pinned);

        int from = IndexOf(pinned, key);

        if (from < 0 || pinned.Count < 2)
        {
            return pinned;
        }

        int to = Math.Clamp(targetIndex, 0, pinned.Count - 1);

        if (from == to)
        {
            return pinned;
        }

        var result = pinned.ToList();
        PinnedApp moved = result[from];

        result.RemoveAt(from);
        result.Insert(to, moved);

        return result;
    }

    private static int IndexOf(IReadOnlyList<PinnedApp> pinned, AppKey key)
    {
        for (int index = 0; index < pinned.Count; index++)
        {
            if (pinned[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }
}
