using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class BlockGeneratorService : IBlockGeneratorService, IReloadableAppData
{
    private readonly IEaseOverridesRepository _ease;
    private readonly Dictionary<string, Dictionary<string, decimal>> _overrides;

    public BlockGeneratorService(IEaseOverridesRepository ease)
    {
        _ease = ease;
        var persisted = ease.Load();
        _overrides = persisted.OverridesByStyle.Count > 0
            ? CloneOverrides(persisted.OverridesByStyle)
            : new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
    }

    public void ReloadFromStore()
    {
        var persisted = _ease.Load();
        _overrides.Clear();
        foreach (var (style, map) in persisted.OverridesByStyle)
            _overrides[style] = new Dictionary<string, decimal>(map, StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureFresh() => ReloadFromStore();

    private static readonly Dictionary<string, BlockDefinition> _defs =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"] = new()
        {
            Label      = "Skinny Fit",
            FitProfile = "Ultra-close fit through hip and thigh. Heavy stretch fabric (>=2% Spandex) required. Negative ease at ankle and knee creates signature silhouette.",
            DefaultEase = new() { ["Waist"]=-2,["Hip"]=-4,["Thigh"]=-2,["Knee"]=-6,["Ankle"]=-8,["Front Rise"]=0,["Back Rise"]=1,["Crotch Depth"]=0,["Inseam"]=-1 },
            Formulas =
            [
                new(){Key="Waist/4",   Equation="(body waist - 2) / 4 = front waist"},
                new(){Key="Hip/4",     Equation="(body hip - 4) / 4 = front hip"},
                new(){Key="Front Rise",Equation="body front rise + 1.0 cm SA"},
                new(){Key="Back Rise", Equation="body back rise + 1.5 cm extra"},
                new(){Key="Inseam",    Equation="body inseam - 1 cm (stretch factor)"},
                new(){Key="Ankle",     Equation="body ankle - 8 cm (stretch opens)"},
                new(){Key="Thigh",     Equation="body thigh - 2 cm (stretch)"},
                new(){Key="Knee",      Equation="body knee - 6 cm (stretch)"},
            ],
        },
        ["slim"] = new()
        {
            Label      = "Slim Fit",
            FitProfile = "Tapered from thigh to ankle. Comfortable through hip with modern narrow hem. Suitable for rigid and stretch denim (>=98% cotton ok).",
            DefaultEase = new() { ["Waist"]=0,["Hip"]=0,["Thigh"]=2,["Knee"]=-2,["Ankle"]=-4,["Front Rise"]=0,["Back Rise"]=1,["Crotch Depth"]=0.5m,["Inseam"]=0 },
            Formulas =
            [
                new(){Key="Waist/4",   Equation="body waist / 4 = front waist (no ease)"},
                new(){Key="Hip/4",     Equation="body hip / 4 = front hip"},
                new(){Key="Thigh",     Equation="body thigh + 2 cm comfort ease"},
                new(){Key="Knee",      Equation="body knee - 2 cm tapered"},
                new(){Key="Ankle",     Equation="body ankle - 4 cm finished opening"},
                new(){Key="Front Rise",Equation="body front rise + 1 cm SA"},
                new(){Key="Back Rise", Equation="body back rise + 1 cm extra"},
                new(){Key="Inseam",    Equation="body inseam (no adjustment)"},
            ],
        },
        ["straight"] = new()
        {
            Label      = "Straight Fit",
            FitProfile = "Classic straight cut from thigh to hem. Equal width at knee and ankle. Timeless silhouette, works with rigid denim (100% cotton).",
            DefaultEase = new() { ["Waist"]=2,["Hip"]=2,["Thigh"]=4,["Knee"]=4,["Ankle"]=0,["Front Rise"]=1,["Back Rise"]=1.5m,["Crotch Depth"]=1,["Inseam"]=0 },
            Formulas =
            [
                new(){Key="Waist/4",   Equation="(body waist + 2) / 4 = front waist"},
                new(){Key="Hip/4",     Equation="(body hip + 2) / 4 = front hip"},
                new(){Key="Thigh",     Equation="body thigh + 4 cm ease"},
                new(){Key="Knee",      Equation="same as thigh width (straight)"},
                new(){Key="Ankle",     Equation="same as knee (no taper)"},
                new(){Key="Front Rise",Equation="body front rise + 1 cm ease"},
                new(){Key="Back Rise", Equation="body back rise + 1.5 cm ease"},
                new(){Key="Inseam",    Equation="body inseam (no adjustment)"},
            ],
        },
        ["bootcut"] = new()
        {
            Label      = "Bootcut Fit",
            FitProfile = "Fitted through thigh, flared from knee to hem. Boot-cut flare starts at or just above the knee. Rigid or mid-stretch denim.",
            DefaultEase = new() { ["Waist"]=2,["Hip"]=2,["Thigh"]=4,["Knee"]=2,["Ankle"]=8,["Front Rise"]=1,["Back Rise"]=2,["Crotch Depth"]=1,["Inseam"]=0 },
            Formulas =
            [
                new(){Key="Waist/4",   Equation="(body waist + 2) / 4 = front waist"},
                new(){Key="Hip/4",     Equation="(body hip + 2) / 4 = front hip"},
                new(){Key="Thigh",     Equation="body thigh + 4 cm ease"},
                new(){Key="Knee",      Equation="body knee + 2 cm ease"},
                new(){Key="Ankle",     Equation="body ankle + 8 cm (flare)"},
                new(){Key="Flare",     Equation="(ankle - knee) / 2 per side seam"},
                new(){Key="Back Rise", Equation="body back rise + 2 cm seat room"},
                new(){Key="Inseam",    Equation="body inseam (no adjustment)"},
            ],
        },
        ["wideLeg"] = new()
        {
            Label      = "Wide Leg Fit",
            FitProfile = "Relaxed straight from hip to hem. Maximum comfort volume. High-rise silhouette typical. Rigid or lightweight denim.",
            DefaultEase = new() { ["Waist"]=4,["Hip"]=6,["Thigh"]=10,["Knee"]=10,["Ankle"]=12,["Front Rise"]=2,["Back Rise"]=2.5m,["Crotch Depth"]=2,["Inseam"]=0 },
            Formulas =
            [
                new(){Key="Waist/4",   Equation="(body waist + 4) / 4 = front waist"},
                new(){Key="Hip/4",     Equation="(body hip + 6) / 4 = front hip"},
                new(){Key="Thigh",     Equation="body thigh + 10 cm (wide)"},
                new(){Key="Knee",      Equation="same as thigh (no taper)"},
                new(){Key="Ankle",     Equation="body ankle + 12 cm (max volume)"},
                new(){Key="Front Rise",Equation="body front rise + 2 cm (high rise)"},
                new(){Key="Back Rise", Equation="body back rise + 2.5 cm"},
                new(){Key="Inseam",    Equation="body inseam (no adjustment)"},
            ],
        },
    };

    public BlockDefinition GetDefinition(string styleKey)
    {
        var key = StyleOptionCatalog.NormalizeStyleKey(styleKey);
        if (_defs.TryGetValue(key, out var d))
            return d;
        return _defs[StyleOptionCatalog.TemplateStyleKey(key)];
    }

    public Dictionary<string, decimal> GetEffectiveEase(string styleKey)
    {
        var def     = GetDefinition(styleKey);
        var result  = new Dictionary<string, decimal>(def.DefaultEase);
        if (_overrides.TryGetValue(styleKey, out var ov))
            foreach (var kv in ov)
                result[kv.Key] = kv.Value;
        return result;
    }

    public void SetEaseOverride(string styleKey, string key, decimal value)
    {
        if (!_overrides.ContainsKey(styleKey))
            _overrides[styleKey] = new(StringComparer.OrdinalIgnoreCase);
        _overrides[styleKey][key] = value;
        PersistOverrides();
    }

    public void ResetEase(string styleKey)
    {
        _overrides.Remove(styleKey);
        PersistOverrides();
    }

    private void PersistOverrides()
    {
        _ease.Save(new EaseOverridesStore
        {
            OverridesByStyle = CloneOverrides(_overrides),
        });
    }

    private static Dictionary<string, Dictionary<string, decimal>> CloneOverrides(
        Dictionary<string, Dictionary<string, decimal>> source)
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (style, map) in source)
            result[style] = new Dictionary<string, decimal>(map, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public GeneratedBlock GenerateBlock(string styleKey)
    {
        var def = GetDefinition(styleKey);
        return new GeneratedBlock { StyleLabel = def.Label, PieceCount = def.Formulas.Count };
    }
}