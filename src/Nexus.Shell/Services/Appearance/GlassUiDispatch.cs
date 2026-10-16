// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Native backdrop events can arrive during detach/close. A WinUI dispatcher
// callback must never let a late COM/XAML exception escape the message loop.
// The caller supplies a current-generation check; rejected queues are inert.
public static class GlassUiDispatch
{
    public static bool TryPost(
        Func<Action, bool> enqueue,
        Func<bool> current,
        Action update,
        Action<Exception> report)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(report);

        void SafelyReport(Exception error)
        {
            try { report(error); }
            catch { /* Diagnostic failure must not become a UI crash. */ }
        }

        try
        {
            return enqueue(() =>
            {
                try
                {
                    if (current()) update();
                }
                catch (Exception error) { SafelyReport(error); }
            });
        }
        catch (Exception error)
        {
            SafelyReport(error);
            return false;
        }
    }
}
