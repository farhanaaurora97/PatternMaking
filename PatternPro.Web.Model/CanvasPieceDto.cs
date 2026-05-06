namespace Pattern.Web.Model;

public class CanvasPieceDto
{
    public string       Name    { get; set; } = string.Empty;
    public string       Cut     { get; set; } = string.Empty;
    public string       Col     { get; set; } = string.Empty;
    public List<int[]>  Pts     { get; set; } = [];
    public List<int[]>? Grain   { get; set; }
    public List<int[]>? Cf      { get; set; }
    public List<int[]>? Notches { get; set; }
    public int          Ox      { get; set; }
    public int          Oy      { get; set; }
}

public class SavePieceRequest
{
    public int          PatternId { get; set; }
    public string       Style   { get; set; } = "skinny";
    public string       Name    { get; set; } = string.Empty;
    public List<int[]>  Pts     { get; set; } = [];
    public int          Ox      { get; set; }
    public int          Oy      { get; set; }
    public List<int[]>? Grain   { get; set; }
    public List<int[]>? Cf      { get; set; }
    public List<int[]>? Notches { get; set; }
}

public class SaveAllPiecesRequest
{
    public int                   PatternId { get; set; }
    public string                Style  { get; set; } = "skinny";
    public List<SavePieceRequest> Pieces { get; set; } = [];
}

public class CreatePieceRequest
{
    public int         PatternId { get; set; }
    public string      Style    { get; set; } = "skinny";
    public string      Name     { get; set; } = string.Empty;
    public string      Category { get; set; } = "Body Panels";
    public string      Cut      { get; set; } = "Cut 2";
    public string      Color    { get; set; } = "#a78bfa";
    public List<int[]> Pts      { get; set; } = [];
    public int         Ox       { get; set; }
    public int         Oy       { get; set; }
}

public class PieceMeasurementsDto
{
    public string       PieceName   { get; set; } = string.Empty;
    public double       Perimeter   { get; set; }
    public double       Area        { get; set; }
    public int          BboxW       { get; set; }
    public int          BboxH       { get; set; }
    public List<double> EdgeLengths { get; set; } = [];
}

public class DraftFromMeasurementsRequest
{
    public string Style { get; set; } = "skinny";
    public string BaseSize { get; set; } = "M";
    public List<string> Sizes { get; set; } = [];
    public decimal Waist { get; set; }
    public decimal Hip { get; set; }
    public decimal FrontRise { get; set; }
    public decimal BackRise { get; set; }
    public decimal Thigh { get; set; }
    public decimal Knee { get; set; }
    public decimal Ankle { get; set; }
    public decimal Inseam { get; set; }
}

public class RecommendSizeRequest
{
    public string BaseSize { get; set; } = "M";
    public decimal Waist { get; set; }
    public decimal Hip { get; set; }
    public decimal FrontRise { get; set; }
    public decimal BackRise { get; set; }
    public decimal Thigh { get; set; }
    public decimal Knee { get; set; }
    public decimal Ankle { get; set; }
    public decimal Inseam { get; set; }
}

public class SaveMeasurementProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Waist { get; set; }
    public decimal Hip { get; set; }
    public decimal FrontRise { get; set; }
    public decimal BackRise { get; set; }
    public decimal Thigh { get; set; }
    public decimal Knee { get; set; }
    public decimal Ankle { get; set; }
    public decimal Inseam { get; set; }
}
