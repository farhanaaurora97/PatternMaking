namespace Pattern.Core.Model;

public static class PieceSeamAllowanceHelper
{
    public static double ResolveEdgeSeamAllowance(PieceDefinition piece, int edgeIndex)
    {
        if (piece.Edges is not null && edgeIndex >= 0 && edgeIndex < piece.Edges.Count)
        {
            var sa = piece.Edges[edgeIndex].SeamAllowance;
            if (sa > 0.0001)
                return sa;
        }

        return piece.SeamAllowance;
    }

    public static bool HasPerEdgeSeamAllowance(PieceDefinition piece)
    {
        if (piece.Edges is null || piece.SeamAllowance <= 0.0001)
            return false;

        return piece.Edges.Any(e => e.SeamAllowance > 0.0001
            && Math.Abs(e.SeamAllowance - piece.SeamAllowance) > 0.0001);
    }

    public static List<double> BuildEdgeOffsets(PieceDefinition piece)
    {
        var n = piece.Points.Count;
        if (n == 0)
            return [];

        var offsets = new List<double>(n);
        for (var i = 0; i < n; i++)
            offsets.Add(ResolveEdgeSeamAllowance(piece, i));
        return offsets;
    }

    public static double EffectiveSeamAllowance(PieceDefinition piece)
    {
        if (HasPerEdgeSeamAllowance(piece))
            return BuildEdgeOffsets(piece).DefaultIfEmpty(piece.SeamAllowance).Max();
        return piece.SeamAllowance;
    }

    public static List<PieceEdge> CloneEdges(IReadOnlyList<PieceEdge>? edges) =>
        edges is null
            ? []
            : edges.Select(e => new PieceEdge
            {
                Kind = e.Kind,
                C1 = e.C1 is null ? null : [e.C1[0], e.C1[1]],
                C2 = e.C2 is null ? null : [e.C2[0], e.C2[1]],
                SeamAllowance = e.SeamAllowance,
            }).ToList();
}
