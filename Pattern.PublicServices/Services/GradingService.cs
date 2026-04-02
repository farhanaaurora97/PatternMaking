using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class GradingService : IGradingService
{
    private static readonly Dictionary<string, (string Label, List<GradingRow> Rows)> _data =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"] = ("Skinny Fit",
        [
            new() { MeasurementPoint="Waist",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Hip",         XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=8    },
            new() { MeasurementPoint="Front Rise",  XS=-0.5m, S=-0.25m,L=0.25m,XL=0.5m, XXL=0.75m},
            new() { MeasurementPoint="Back Rise",   XS=-0.75m,S=-0.4m, L=0.4m, XL=0.75m,XXL=1    },
            new() { MeasurementPoint="Thigh",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Knee",        XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Ankle",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=4.5m },
            new() { MeasurementPoint="Inseam",      XS=-2,    S=-1,    L=1,    XL=2,    XXL=2    },
        ]),
        ["slim"] = ("Slim Fit",
        [
            new() { MeasurementPoint="Waist",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Hip",         XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=8    },
            new() { MeasurementPoint="Front Rise",  XS=-0.5m, S=-0.25m,L=0.25m,XL=0.5m, XXL=0.75m},
            new() { MeasurementPoint="Back Rise",   XS=-0.75m,S=-0.4m, L=0.4m, XL=0.75m,XXL=1    },
            new() { MeasurementPoint="Thigh",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Knee",        XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Ankle",       XS=-2.5m, S=-1.5m, L=1.5m, XL=2.5m, XXL=4    },
            new() { MeasurementPoint="Inseam",      XS=-2,    S=-1,    L=1,    XL=2,    XXL=2    },
        ]),
        ["straight"] = ("Straight Fit",
        [
            new() { MeasurementPoint="Waist",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Hip",         XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=8    },
            new() { MeasurementPoint="Front Rise",  XS=-0.5m, S=-0.25m,L=0.25m,XL=0.5m, XXL=0.75m},
            new() { MeasurementPoint="Back Rise",   XS=-0.75m,S=-0.4m, L=0.4m, XL=0.75m,XXL=1    },
            new() { MeasurementPoint="Thigh",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Knee",        XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Ankle",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Inseam",      XS=-2,    S=-1,    L=1,    XL=2,    XXL=2    },
        ]),
        ["bootcut"] = ("Bootcut Fit",
        [
            new() { MeasurementPoint="Waist",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Hip",         XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=8    },
            new() { MeasurementPoint="Front Rise",  XS=-0.5m, S=-0.25m,L=0.25m,XL=0.5m, XXL=0.75m},
            new() { MeasurementPoint="Back Rise",   XS=-1,    S=-0.5m, L=0.5m, XL=1,    XXL=1.5m },
            new() { MeasurementPoint="Thigh",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Knee",        XS=-2.5m, S=-1.5m, L=1.5m, XL=2.5m, XXL=4    },
            new() { MeasurementPoint="Ankle",       XS=-2,    S=-1,    L=1,    XL=2,    XXL=3    },
            new() { MeasurementPoint="Inseam",      XS=-2,    S=-1,    L=1,    XL=2,    XXL=2    },
        ]),
        ["wideLeg"] = ("Wide Leg Fit",
        [
            new() { MeasurementPoint="Waist",       XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=7    },
            new() { MeasurementPoint="Hip",         XS=-5,    S=-2.5m, L=2.5m, XL=5,    XXL=8    },
            new() { MeasurementPoint="Front Rise",  XS=-0.5m, S=-0.25m,L=0.25m,XL=0.5m, XXL=0.75m},
            new() { MeasurementPoint="Back Rise",   XS=-0.75m,S=-0.4m, L=0.4m, XL=0.75m,XXL=1    },
            new() { MeasurementPoint="Thigh",       XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Knee",        XS=-4,    S=-2,    L=2,    XL=4,    XXL=6    },
            new() { MeasurementPoint="Ankle",       XS=-3,    S=-1.5m, L=1.5m, XL=3,    XXL=5    },
            new() { MeasurementPoint="Inseam",      XS=-2,    S=-1,    L=1,    XL=2,    XXL=2    },
        ]),
    };

    public IReadOnlyList<GradingRow> GetGradingTable(string styleKey) =>
        _data.TryGetValue(styleKey, out var d) ? d.Rows : _data["skinny"].Rows;

    public string GetStyleLabel(string styleKey) =>
        _data.TryGetValue(styleKey, out var d) ? d.Label : "Skinny Fit";

    public string ExportCsv(string styleKey)
    {
        var rows  = GetGradingTable(styleKey);
        var lines = new List<string> { "Measurement,XS,S,M(Base),L,XL,XXL" };
        foreach (var r in rows)
            lines.Add($"{r.MeasurementPoint},{r.XS},{r.S},0,+{r.L},+{r.XL},+{r.XXL}");
        return string.Join("\n", lines);
    }
}