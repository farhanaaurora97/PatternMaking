using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IBlockGeneratorService
{
    BlockDefinition              GetDefinition(string styleKey);
    Dictionary<string, decimal>  GetEffectiveEase(string styleKey);
    void                         SetEaseOverride(string styleKey, string key, decimal value);
    void                         ResetEase(string styleKey);
    GeneratedBlock               GenerateBlock(string styleKey);
}

