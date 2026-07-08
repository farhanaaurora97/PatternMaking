namespace PatternPro.Desktop.Helpers;

internal static class PieceGroupHelper
{
    public static string GroupFor(string name) => name switch
    {
        "Front Leg" or "Back Leg" or "Waistband" or "Waist Tab" or "Flare Insert" => "Body Panels",
        "Fly Facing" or "Fly Shield" => "Closures",
        "Coin Pocket" or "Front Pocket Bag" or "Pocket Bag" or
            "Side Pocket Bag" or "Back Patch Pocket" or "Back Pocket" => "Pockets",
        _ => "Hardware & Details",
    };

    public static int GroupOrder(string group) => group switch
    {
        "Body Panels" => 0,
        "Closures" => 1,
        "Pockets" => 2,
        _ => 3,
    };

    public static string DefaultColor(string name) =>
        ColorMap.TryGetValue(name, out var c) ? c : "#3d3d3d";

    private static readonly Dictionary<string, string> ColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Front Leg"] = "#1a1a6e",
        ["Back Leg"] = "#1a1a6e",
        ["Waistband"] = "#2626a0",
        ["Fly Facing"] = "#534AB7",
        ["Fly Shield"] = "#534AB7",
        ["Coin Pocket"] = "#0F6E56",
        ["Front Pocket Bag"] = "#0F6E56",
        ["Pocket Bag"] = "#0F6E56",
        ["Back Patch Pocket"] = "#854F0B",
        ["Back Pocket"] = "#854F0B",
        ["Belt Loop"] = "#854F0B",
        ["Side Pocket Bag"] = "#0F6E56",
        ["Flare Insert"] = "#7B2C8E",
        ["Waist Tab"] = "#2626a0",
    };
}
