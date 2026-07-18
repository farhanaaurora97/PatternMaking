namespace Pattern.Core.Model;

public enum ExportPurpose
{
    /// <summary>Full graded package for plotter/CAM — requires production certification.</summary>
    Factory = 0,

    /// <summary>Base-size review package for CLO3D import (no certification required).</summary>
    CloReview = 1,

    /// <summary>Internal draft export (no certification).</summary>
    Draft = 2,
}
