namespace Nexus.Shell.Services;

// Tracks values actually committed to one native acrylic controller.
// A new controller must receive the full recipe even if its predecessor
// used the same colors. Failed partial writes are never recorded as success.
internal sealed class GlassTintRetention
{
    private GlassRecipe? _committed;

    internal bool RequiresApply(GlassRecipe recipe, bool force = false)
        => force || _committed != recipe;

    internal void Commit(GlassRecipe recipe) => _committed = recipe;

    internal void Reset() => _committed = null;
}
