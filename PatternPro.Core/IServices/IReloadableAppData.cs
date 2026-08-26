namespace PatternPro.Core.IServices;

/// <summary>
/// Singleton services that cache PostgreSQL/JSON data must reload before reads
/// so team Desktop clients see changes from other PCs without restarting the app.
/// </summary>
public interface IReloadableAppData
{
    void ReloadFromStore();
}
