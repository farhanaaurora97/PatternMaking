namespace Pattern.Web.Model;

public class LibraryEntryViewModel
{
    public int    Id         { get; set; }
    public string Code      { get; set; } = "";
    public string Name      { get; set; } = "";
    public string Style     { get; set; } = "";
    public string StyleKey  { get; set; } = "";
    public string BaseSize  { get; set; } = "";
    public string Category  { get; set; } = "";
    public string Status    { get; set; } = "";
    public string Date      { get; set; } = "";
    public bool   HasCanvas { get; set; }
    public int    PieceCount{ get; set; }
}
