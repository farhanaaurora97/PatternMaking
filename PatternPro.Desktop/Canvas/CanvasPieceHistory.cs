using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

internal sealed class CanvasPieceHistory
{
    private const int MaxDepth = 60;
    private readonly Stack<PieceSnapshot> _stack = new();

    public bool CanUndo => _stack.Count > 0;

    public void Push(CoreModel.PieceDefinition piece, int index)
    {
        _stack.Push(PieceSnapshot.Capture(piece, index));
        while (_stack.Count > MaxDepth)
        {
            var items = _stack.ToArray();
            _stack.Clear();
            for (var i = items.Length - 2; i >= 0; i--)
                _stack.Push(items[i]);
        }
    }

    public bool TryPop(IList<CoreModel.PieceDefinition> pieces, out int restoredIndex)
    {
        restoredIndex = -1;
        if (_stack.Count == 0) return false;

        var snap = _stack.Pop();
        if (snap.Index < 0 || snap.Index >= pieces.Count) return false;

        snap.Apply(pieces[snap.Index]);
        restoredIndex = snap.Index;
        return true;
    }

    public void Clear() => _stack.Clear();

    private sealed class PieceSnapshot
    {
        public int Index { get; init; }
        public List<int[]> Points { get; init; } = [];
        public List<CoreModel.PieceEdge>? Edges { get; init; }
        public int OffsetX { get; init; }
        public int OffsetY { get; init; }
        public List<int[]>? Grain { get; init; }
        public List<int[]>? Cf { get; init; }
        public List<int[]>? Notches { get; init; }
        public List<CoreModel.PieceInternalLine>? InternalLines { get; init; }

        public static PieceSnapshot Capture(CoreModel.PieceDefinition piece, int index) =>
            new()
            {
                Index = index,
                Points = piece.Points.Select(p => new[] { p[0], p[1] }).ToList(),
                Edges = piece.Edges?.Select(e => new CoreModel.PieceEdge
                {
                    Kind = e.Kind,
                    C1 = e.C1 is null ? null : new[] { e.C1[0], e.C1[1] },
                    C2 = e.C2 is null ? null : new[] { e.C2[0], e.C2[1] },
                    SeamAllowance = e.SeamAllowance,
                }).ToList(),
                OffsetX = piece.OffsetX,
                OffsetY = piece.OffsetY,
                Grain = piece.Grain?.Select(p => new[] { p[0], p[1] }).ToList(),
                Cf = piece.Cf?.Select(p => new[] { p[0], p[1] }).ToList(),
                Notches = piece.Notches?.Select(p => new[] { p[0], p[1] }).ToList(),
                InternalLines = piece.InternalLines is null ? null : CoreModel.PieceInternalLine.CloneList(piece.InternalLines),
            };

        public void Apply(CoreModel.PieceDefinition piece)
        {
            piece.Points = Points.Select(p => new[] { p[0], p[1] }).ToList();
            piece.Edges = Edges?.Select(e => new CoreModel.PieceEdge
            {
                Kind = e.Kind,
                C1 = e.C1 is null ? null : new[] { e.C1[0], e.C1[1] },
                C2 = e.C2 is null ? null : new[] { e.C2[0], e.C2[1] },
                SeamAllowance = e.SeamAllowance,
            }).ToList();
            piece.OffsetX = OffsetX;
            piece.OffsetY = OffsetY;
            piece.Grain = Grain?.Select(p => new[] { p[0], p[1] }).ToList();
            piece.Cf = Cf?.Select(p => new[] { p[0], p[1] }).ToList();
            piece.Notches = Notches?.Select(p => new[] { p[0], p[1] }).ToList();
            piece.InternalLines = InternalLines is null ? null : CoreModel.PieceInternalLine.CloneList(InternalLines);
        }
    }
}
