using Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasPieceTransform
{
    public static void MirrorHorizontal(PieceDefinition piece)
    {
        if (piece.Points.Count < 1) return;
        PiecePathBuilder.EnsureEdges(piece);

        var cx = piece.Points.Average(p => p[0]);
        MirrorX(piece, cx);
    }

    public static void MirrorVertical(PieceDefinition piece)
    {
        if (piece.Points.Count < 1) return;
        PiecePathBuilder.EnsureEdges(piece);

        var cy = piece.Points.Average(p => p[1]);
        MirrorY(piece, cy);
    }

    public static void Rotate90Clockwise(PieceDefinition piece)
    {
        if (piece.Points.Count < 1) return;
        PiecePathBuilder.EnsureEdges(piece);

        var cx = piece.Points.Average(p => p[0]);
        var cy = piece.Points.Average(p => p[1]);

        foreach (var pt in piece.Points)
        {
            var dx = pt[0] - cx;
            var dy = pt[1] - cy;
            pt[0] = (int)Math.Round(cx + dy);
            pt[1] = (int)Math.Round(cy - dx);
        }

        RotateEdges90(piece, cx, cy);
        TransformAuxPoints(piece, cx, cy, RotatePoint90);
    }

    private static void MirrorX(PieceDefinition piece, double axisLocalX)
    {
        foreach (var pt in piece.Points)
            pt[0] = (int)Math.Round(2 * axisLocalX - pt[0]);

        if (piece.Edges is not null)
        {
            for (var i = 0; i < piece.Edges.Count; i++)
            {
                var edge = piece.Edges[i];
                if (edge.C1 is { Length: >= 2 } c1)
                    c1[0] = (int)Math.Round(2 * axisLocalX - c1[0]);
                if (edge.C2 is { Length: >= 2 } c2)
                    c2[0] = (int)Math.Round(2 * axisLocalX - c2[0]);
            }
        }

        TransformAuxPoints(piece, axisLocalX, 0, (x, y, ax, ay) =>
            ((int)Math.Round(2 * ax - x), y));
    }

    private static void MirrorY(PieceDefinition piece, double axisLocalY)
    {
        foreach (var pt in piece.Points)
            pt[1] = (int)Math.Round(2 * axisLocalY - pt[1]);

        if (piece.Edges is not null)
        {
            for (var i = 0; i < piece.Edges.Count; i++)
            {
                var edge = piece.Edges[i];
                if (edge.C1 is { Length: >= 2 } c1)
                    c1[1] = (int)Math.Round(2 * axisLocalY - c1[1]);
                if (edge.C2 is { Length: >= 2 } c2)
                    c2[1] = (int)Math.Round(2 * axisLocalY - c2[1]);
            }
        }

        TransformAuxPoints(piece, 0, axisLocalY, (x, y, ax, ay) =>
            (x, (int)Math.Round(2 * ay - y)));
    }

    private static void RotateEdges90(PieceDefinition piece, double cx, double cy)
    {
        if (piece.Edges is null) return;
        foreach (var edge in piece.Edges)
        {
            if (edge.C1 is { Length: >= 2 } c1)
            {
                var (nx, ny) = RotatePoint90(c1[0], c1[1], cx, cy);
                c1[0] = nx;
                c1[1] = ny;
            }

            if (edge.C2 is { Length: >= 2 } c2)
            {
                var (nx, ny) = RotatePoint90(c2[0], c2[1], cx, cy);
                c2[0] = nx;
                c2[1] = ny;
            }
        }
    }

    private static void TransformAuxPoints(
        PieceDefinition piece,
        double axisX,
        double axisY,
        Func<int, int, double, double, (int X, int Y)> map)
    {
        if (piece.Grain is not null)
        {
            foreach (var g in piece.Grain)
            {
                if (g.Length < 2) continue;
                var (nx, ny) = map(g[0], g[1], axisX, axisY);
                g[0] = nx;
                g[1] = ny;
            }
        }

        if (piece.Cf is not null)
        {
            foreach (var c in piece.Cf)
            {
                if (c.Length < 2) continue;
                var (nx, ny) = map(c[0], c[1], axisX, axisY);
                c[0] = nx;
                c[1] = ny;
            }
        }

        if (piece.Notches is not null)
        {
            foreach (var n in piece.Notches)
            {
                if (n.Length < 2) continue;
                var (nx, ny) = map(n[0], n[1], axisX, axisY);
                n[0] = nx;
                n[1] = ny;
            }
        }

        CanvasInternalLineHelper.TransformLines(piece, (x, y) => map(x, y, axisX, axisY));
    }

    private static (int X, int Y) RotatePoint90(int x, int y, double cx, double cy)
    {
        var dx = x - cx;
        var dy = y - cy;
        return ((int)Math.Round(cx + dy), (int)Math.Round(cy - dx));
    }
}
