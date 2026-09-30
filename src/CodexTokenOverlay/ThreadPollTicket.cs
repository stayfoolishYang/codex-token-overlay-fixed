namespace CodexTokenOverlay;

// An earlier read must not overwrite a later selection, even after A -> B -> A.
internal sealed record ThreadPollTicket(long Epoch, long RouteVersion, bool IsPinned)
{
    public bool CanApply(long epoch, ActiveThreadRouteStatus route, bool isPinned, TokenSnapshot? snapshot)
    {
        if (Epoch != epoch || IsPinned != isPinned)
        {
            return false;
        }
        if (IsPinned)
        {
            return true;
        }
        return RouteVersion == route.Version
            && (snapshot is null || string.Equals(snapshot.ThreadId, route.ThreadId, StringComparison.OrdinalIgnoreCase));
    }
}
