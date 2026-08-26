using CoreModel = Pattern.Core.Model;
using Pattern.Core.Model;
using PatternPro.Business.Services;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasPieceCloneHelper
{
    private const int DuplicateShiftPx = 36;

    public static CoreModel.PieceDefinition ClonePiece(
        CoreModel.PieceDefinition source,
        string newName,
        int shiftX = DuplicateShiftPx,
        int shiftY = DuplicateShiftPx)
    {
        var copy = new CoreModel.PieceDefinition
        {
            Name = newName,
            PieceNumber = source.PieceNumber,
            Material = source.Material,
            OnFold = source.OnFold,
            Cut = source.Cut,
            Color = source.Color,
            Category = source.Category,
            GrainLine = source.GrainLine,
            Description = source.Description,
            Points = source.Points.Select(p => new[] { p[0], p[1] }).ToList(),
            Edges = source.Edges is null ? null : PieceSeamAllowanceHelper.CloneEdges(source.Edges),
            Grain = source.Grain?.Select(p => new[] { p[0], p[1] }).ToList(),
            Cf = source.Cf?.Select(p => new[] { p[0], p[1] }).ToList(),
            Notches = source.Notches?.Select(p => new[] { p[0], p[1] }).ToList(),
            InternalLines = CoreModel.PieceInternalLine.CloneList(source.InternalLines),
            OffsetX = source.OffsetX + shiftX,
            OffsetY = source.OffsetY + shiftY,
            SeamAllowance = source.SeamAllowance,
            SeamAllowanceJoin = source.SeamAllowanceJoin,
        };
        return copy;
    }

    public static CoreModel.PieceDefinition OffsetOutlineCopy(
        CoreModel.PieceDefinition source,
        string newName,
        double offsetCm,
        int shiftX = DuplicateShiftPx,
        int shiftY = DuplicateShiftPx)
    {
        var copy = ClonePiece(source, newName, shiftX, shiftY);
        if (copy.Points.Count < 3 || Math.Abs(offsetCm) < 0.001)
            return copy;

        var px = CanvasUnits.ToPixels(offsetCm);
        var ox = copy.OffsetX;
        var oy = copy.OffsetY;
        var basePts = copy.Points
            .Select(pt => new SeamAllowanceOffset.Pt(pt[0] + ox, pt[1] + oy))
            .ToList();
        var offsets = PieceSeamAllowanceHelper.BuildEdgeOffsets(copy);
        var outPts = SeamAllowanceOffset.OffsetClosed(
            basePts,
            px,
            SeamAllowanceOffset.ParseJoin(copy.SeamAllowanceJoin),
            offsets);

        if (outPts.Count < 3)
            return copy;

        var minX = outPts.Min(p => p.X);
        var minY = outPts.Min(p => p.Y);
        copy.OffsetX = (int)Math.Round(minX);
        copy.OffsetY = (int)Math.Round(minY);
        copy.Points = outPts
            .Select(p => new[] { (int)Math.Round(p.X - minX), (int)Math.Round(p.Y - minY) })
            .ToList();
        copy.Edges = null;
        PiecePathBuilder.EnsureEdges(copy);
        return copy;
    }

    public static string NextCopyName(IReadOnlyList<CoreModel.PieceDefinition> pieces, string baseName)
    {
        var stem = baseName.Trim();
        if (stem.EndsWith(" Copy", StringComparison.OrdinalIgnoreCase))
            stem = stem[..^5].TrimEnd();

        var candidate = $"{stem} Copy";
        var n = 2;
        while (pieces.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{stem} Copy {n}";
            n++;
        }

        return candidate;
    }
}
