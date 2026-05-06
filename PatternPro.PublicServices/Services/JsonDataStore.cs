using System.Text.Json;
using System.Text.Json.Serialization;
using Pattern.Core.Model;

namespace Pattern.PublicServices.Services;

public class JsonDataStore
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _piecesPath;
    private readonly string _patternsPath;

    public JsonDataStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _piecesPath   = Path.Combine(dataDirectory, "pieces.json");
        _patternsPath = Path.Combine(dataDirectory, "patterns.json");
    }

    public PiecesStore LoadPieces()
    {
        if (!File.Exists(_piecesPath)) return new PiecesStore();
        try
        {
            var json = File.ReadAllText(_piecesPath);
            return JsonSerializer.Deserialize<PiecesStore>(json, _opts) ?? new PiecesStore();
        }
        catch { return new PiecesStore(); }
    }

    public void SavePieces(PiecesStore store)
    {
        try { File.WriteAllText(_piecesPath, JsonSerializer.Serialize(store, _opts)); }
        catch { }
    }

    public PatternsStore? LoadPatternsStore()
    {
        if (!File.Exists(_patternsPath)) return null;
        try
        {
            var json = File.ReadAllText(_patternsPath);
            return JsonSerializer.Deserialize<PatternsStore>(json, _opts);
        }
        catch { return null; }
    }

    public void SavePatterns(IEnumerable<Core.Model.Pattern> patterns, int nextId)
    {
        try
        {
            var wrapper = new PatternsStore { NextId = nextId, Patterns = patterns.ToList() };
            File.WriteAllText(_patternsPath, JsonSerializer.Serialize(wrapper, _opts));
        }
        catch { }
    }
}

public class PiecesStore
{
    public Dictionary<string, List<PieceDefinition>> StyleGeometry   { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int,    List<PieceDefinition>> PatternGeometry { get; set; } = new();
}

public class PatternsStore
{
    public int                      NextId   { get; set; } = 22;
    public List<Core.Model.Pattern> Patterns { get; set; } = [];
}
