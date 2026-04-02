using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

public interface IPieceService
{
    StyleDefinition              GetStyleDefinition(string styleKey);
    IReadOnlyList<string>        GetPieceList(string styleKey);
    IReadOnlyList<PieceDefinition> GetPieceDefinitions();
    IReadOnlyList<int[]>         GetBasePiecePoints();
}