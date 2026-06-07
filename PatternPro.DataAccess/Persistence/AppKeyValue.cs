namespace PatternPro.DataAccess.Persistence;

/// <summary>Generic key/value settings and JSON blobs (e.g. pieces store).</summary>
public class AppKeyValue
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
