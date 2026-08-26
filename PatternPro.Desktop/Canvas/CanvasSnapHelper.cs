using CoreModel = Pattern.Core.Model;



namespace PatternPro.Desktop.Canvas;



public enum SnapKind

{

    None,

    Grid,

    Vertex,

    Midpoint,

    Edge,

}



internal readonly record struct SnapResult(float X, float Y, SnapKind Kind);



internal static class CanvasSnapHelper

{

    private const float SnapScreenPx = 12f;



    public static SnapResult SnapWorld(

        float wx,

        float wy,

        CanvasEditorOptions opts,

        IReadOnlyList<CoreModel.PieceDefinition>? pieces,

        float scale)

    {

        if (!opts.SnapEnabled)

            return new SnapResult(wx, wy, SnapKind.None);



        var snapWorld = SnapScreenPx / Math.Max(scale, 0.01f);

        var snapWorld2 = snapWorld * snapWorld;



        if (opts.SnapToGeometry && pieces is { Count: > 0 })

        {

            var best = new SnapResult(wx, wy, SnapKind.None);

            var bestDist2 = float.MaxValue;



            foreach (var piece in pieces)

            {

                for (var vi = 0; vi < piece.Points.Count; vi++)

                {

                    var pt = piece.Points[vi];

                    if (pt.Length < 2) continue;

                    var vx = pt[0] + piece.OffsetX;

                    var vy = pt[1] + piece.OffsetY;

                    var d2 = Dist2(wx, wy, vx, vy);

                    if (d2 >= bestDist2 || d2 > snapWorld2) continue;

                    bestDist2 = d2;

                    best = new SnapResult(vx, vy, SnapKind.Vertex);

                }



                if (piece.Points.Count < 2) continue;

                PiecePathBuilder.EnsureEdges(piece);

                for (var ei = 0; ei < piece.Points.Count; ei++)

                {

                    var edgePts = PiecePathBuilder.TessellateEdge(piece, ei);

                    for (var s = 1; s < edgePts.Count; s++)

                    {

                        var ax = edgePts[s - 1].X + piece.OffsetX;

                        var ay = edgePts[s - 1].Y + piece.OffsetY;

                        var bx = edgePts[s].X + piece.OffsetX;

                        var by = edgePts[s].Y + piece.OffsetY;



                        var mx = (ax + bx) / 2f;

                        var my = (ay + by) / 2f;

                        var midD2 = Dist2(wx, wy, mx, my);

                        if (midD2 < bestDist2 && midD2 <= snapWorld2)

                        {

                            bestDist2 = midD2;

                            best = new SnapResult(mx, my, SnapKind.Midpoint);

                        }



                        var (t, d) = CanvasGeometryHelper.ClosestOnSegment(wx, wy, ax, ay, bx, by);

                        var edgeD2 = d * d;

                        if (edgeD2 >= bestDist2 || edgeD2 > snapWorld2) continue;

                        bestDist2 = edgeD2;

                        best = new SnapResult(ax + t * (bx - ax), ay + t * (by - ay), SnapKind.Edge);

                    }

                }

            }



            if (best.Kind != SnapKind.None)

                return best;

        }



        var gridPx = (float)(opts.SnapGridCm * CanvasUnits.PixelsPerCm);

        if (gridPx >= 0.5f)

        {

            return new SnapResult(

                MathF.Round(wx / gridPx) * gridPx,

                MathF.Round(wy / gridPx) * gridPx,

                SnapKind.Grid);

        }



        return new SnapResult(wx, wy, SnapKind.None);

    }



    private static float Dist2(float ax, float ay, float bx, float by)

    {

        var dx = ax - bx;

        var dy = ay - by;

        return dx * dx + dy * dy;

    }

}

