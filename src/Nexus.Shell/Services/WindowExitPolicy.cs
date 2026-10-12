namespace Nexus.Shell.Services;

// Explicit user close is terminal until its bounded native close action runs.
// A WM_SIZE restore notification may cancel an in-flight minimize animation,
// but it must never cancel an accepted close operation.
internal static class WindowExitPolicy
{
    internal static bool AcceptRestore(bool disposed, bool closing) => !disposed && !closing;
}
