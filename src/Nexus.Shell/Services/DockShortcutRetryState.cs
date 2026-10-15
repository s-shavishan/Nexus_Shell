namespace Nexus.Shell.Services;

// COM ShellLink resolution may fail while Explorer/Windows is starting or
// recovering. The dock must retry without doing filesystem/COM work on the
// XAML/WinEvent dispatch path and without starting one thread per poll.
public readonly record struct DockShortcutRetryState(int Failures, long NextAttemptTick)
{
    public bool CanAttempt(long now) => now >= NextAttemptTick;

    public DockShortcutRetryState Failed(long now)
    {
        int failures = Math.Min(Failures + 1, 6);
        long delay = failures switch
        {
            1 => 2000,
            2 => 10000,
            3 => 30000,
            4 => 60000,
            _ => 120000
        };
        return new(failures, now > long.MaxValue - delay ? long.MaxValue : now + delay);
    }
}
