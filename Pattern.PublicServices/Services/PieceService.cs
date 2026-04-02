using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class PieceService : IPieceService
{
    private static readonly Dictionary<string, StyleDefinition> _styles =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"]   = new() { Label="Skinny Fit",   PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["slim"]     = new() { Label="Slim Fit",     PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["straight"] = new() { Label="Straight Fit", PieceCount=8, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["bootcut"]  = new() { Label="Bootcut Fit",  PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop","Flare Insert"] },
        ["wideLeg"]  = new() { Label="Wide Leg Fit", PieceCount=8, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Side Pocket Bag","Back Patch Pocket","Belt Loop","Waist Tab"] },
    };

    private static readonly IReadOnlyList<PieceDefinition> _defs =
    [
        new() { Name="Front Leg",     Cut="Cut 2", Color="#1a1a6e", OffsetX=30,   OffsetY=20,  Points=[[142,60],[242,60],[265,155],[250,185],[248,360],[178,380],[108,360],[106,185],[91,155]],   Grain=[[190,95],[190,345]],  Cf=[[178,60],[178,380]],    Notches=[[174,60],[182,60],[106,250],[250,250]] },
        new() { Name="Back Leg",      Cut="Cut 2", Color="#1a1a6e", OffsetX=310,  OffsetY=20,  Points=[[100,60],[260,60],[295,85],[310,155],[280,190],[278,360],[178,385],[78,360],[76,190],[46,155]], Grain=[[190,95],[190,345]],  Cf=[[178,60],[178,385]],    Notches=[[174,60],[182,60],[76,270],[278,270]] },
        new() { Name="Waistband",     Cut="Cut 1", Color="#2626a0", OffsetX=620,  OffsetY=20,  Points=[[60,60],[380,60],[380,110],[60,110]],              Grain=[[210,63],[210,107]], Cf=[[220,60],[220,110]],    Notches=[[219,60],[221,60]] },
        new() { Name="Fly Facing",    Cut="Cut 2", Color="#534AB7", OffsetX=1060, OffsetY=20,  Points=[[60,60],[110,60],[110,190],[80,210],[60,190]],      Grain=[[84,70],[84,195]],   Cf=null,                    Notches=[[85,60],[85,100]] },
        new() { Name="Fly Shield",    Cut="Cut 1", Color="#534AB7", OffsetX=1195, OffsetY=20,  Points=[[60,60],[95,60],[95,200],[60,200]],                 Grain=[[76,70],[76,190]],   Cf=null,                    Notches=[[77,60],[77,100]] },
        new() { Name="Coin Pocket",   Cut="Cut 2", Color="#0F6E56", OffsetX=1320, OffsetY=20,  Points=[[60,60],[120,60],[120,110],[80,125],[60,110]],      Grain=[[88,65],[88,120]],   Cf=null,                    Notches=[[90,60]] },
        new() { Name="Pocket Bag",    Cut="Cut 2", Color="#0F6E56", OffsetX=1060, OffsetY=260, Points=[[60,60],[160,60],[160,200],[80,220],[60,180]],      Grain=[[108,70],[108,210]], Cf=null,                    Notches=[[60,110],[160,110]] },
        new() { Name="Back Pocket",   Cut="Cut 2", Color="#854F0B", OffsetX=1320, OffsetY=260, Points=[[60,60],[160,60],[165,155],[55,155]],              Grain=[[112,70],[112,148]], Cf=[[112,60],[112,155]],    Notches=[[111,60],[113,60]] },
        new() { Name="Belt Loop",     Cut="Cut 8", Color="#854F0B", OffsetX=1440, OffsetY=260, Points=[[60,60],[85,60],[85,160],[60,160]],                 Grain=[[72,70],[72,150]],   Cf=null,                    Notches=[] },
    ];

    public StyleDefinition GetStyleDefinition(string styleKey) =>
        _styles.TryGetValue(styleKey, out var s) ? s : _styles["skinny"];

    public IReadOnlyList<string> GetPieceList(string styleKey) =>
        GetStyleDefinition(styleKey).PieceList;

    public IReadOnlyList<PieceDefinition> GetPieceDefinitions() => _defs;

    public IReadOnlyList<int[]> GetBasePiecePoints() => _defs[0].Points;
}