namespace Pattern.Core.Model;

/// <summary>One bottom-wear pattern row (dashboard / CRUD).</summary>
public class Pattern
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string BaseSize { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public string Status { get; set; } = "Draft";
    public string Date { get; set; } = string.Empty;
    public string Designer { get; set; } = string.Empty;

    /// <summary>Product line / pant type (e.g. Denim, Chinos, Trousers).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>When the pattern row was created (used for week-over-week stats).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional collection / milestone deadline.</summary>
    public DateTime? DueDate { get; set; }
}
