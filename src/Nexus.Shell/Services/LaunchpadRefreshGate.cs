namespace Nexus.Shell.Services;

// UI-thread-owned admission control for Launchpad's catalog refresh. A manual
// refresh during an existing scan is retained (and repeated clicks coalesce)
// rather than being silently lost or starting concurrent shortcut scans.
public sealed class LaunchpadRefreshGate
{
    private bool _running, _pendingForce;

    public bool PendingForce => _pendingForce;

    // Returns whether work should start and whether the actual installed-app
    // catalog must be rescanned rather than returning an existing cached task.
    public (bool Started, bool Force) Request(bool force)
    {
        if (_running)
        {
            _pendingForce |= force;
            return (false, false);
        }
        _running = true;
        bool requested = force || _pendingForce;
        _pendingForce = false;
        return (true, requested);
    }

    public bool Complete()
    {
        if (!_running) throw new InvalidOperationException("No Launchpad refresh is in progress.");
        _running = false;
        return _pendingForce;
    }
}
