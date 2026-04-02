using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class SizeChartService : ISizeChartService
{
    private static readonly IReadOnlyList<SizeRow> _rows =
    [
        new() { MeasurementPoint = "Waist",       XS=60,  S=64,  M=68,  L=72,  XL=76,  XXL=80  },
        new() { MeasurementPoint = "Hip",         XS=84,  S=88,  M=92,  L=96,  XL=100, XXL=106 },
        new() { MeasurementPoint = "Front Rise",  XS=25,  S=25.5m,M=26, L=26.5m,XL=27, XXL=27.5m},
        new() { MeasurementPoint = "Back Rise",   XS=34,  S=35,  M=36,  L=37,  XL=38,  XXL=39  },
        new() { MeasurementPoint = "Crotch Depth",XS=26,  S=27,  M=28,  L=29,  XL=30,  XXL=31  },
        new() { MeasurementPoint = "Thigh",       XS=50,  S=53,  M=56,  L=59,  XL=62,  XXL=66  },
        new() { MeasurementPoint = "Knee",        XS=34,  S=36,  M=38,  L=40,  XL=42,  XXL=44  },
        new() { MeasurementPoint = "Ankle",       XS=29,  S=31,  M=33,  L=35,  XL=37,  XXL=39  },
        new() { MeasurementPoint = "Inseam",      XS=77,  S=78,  M=79,  L=80,  XL=80,  XXL=80  },
        new() { MeasurementPoint = "Outseam",     XS=103, S=104.5m,M=106,L=107.5m,XL=109,XXL=110},
    ];

    public IReadOnlyList<SizeRow> GetAll() => _rows;

    public string ExportCsv()
    {
        var lines = new List<string> { "Measurement,XS,S,M,L,XL,XXL" };
        foreach (var r in _rows)
            lines.Add($"{r.MeasurementPoint},{r.XS},{r.S},{r.M},{r.L},{r.XL},{r.XXL}");
        return string.Join("\n", lines);
    }
}