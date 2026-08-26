namespace PatternPro.Desktop.Canvas;



public sealed class CanvasEditorOptions

{

    public bool SnapEnabled { get; set; } = true;



    /// <summary>When snap is on, also snap to vertices, edge midpoints, and edges.</summary>

    public bool SnapToGeometry { get; set; } = true;



    /// <summary>Grid snap interval in centimeters.</summary>

    public double SnapGridCm { get; set; } = 1.0;



    public bool ShowCmUnits { get; set; } = true;



    /// <summary>Mirror edits across the vertical symmetry axis on the selected piece.</summary>

    public bool SymmetryEnabled { get; set; }



    /// <summary>World X of the symmetry axis. Set when symmetry is enabled.</summary>

    public float? SymmetryAxisWorldX { get; set; }

}

