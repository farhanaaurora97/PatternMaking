using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;

namespace PatternPro.Tests;

public class PatternAutoRefineServiceTests
{
    [Fact]
    public void Refine_AppliesSeamAllowanceToBodyPanels()
    {
        var pieces = new List<PieceDefinition>
        {
            TestPieceFactory.Rect("Front Leg", seamAllowance: 0),
            TestPieceFactory.Rect("Back Leg", seamAllowance: 0),
            TestPieceFactory.Rect("Waistband", 80, seamAllowance: 0),
        };

        PatternAutoRefineService.Refine(pieces, "slim");

        Assert.True(pieces[0].SeamAllowance > 0);
        Assert.True(pieces[2].SeamAllowance > 0);
    }

    [Fact]
    public void BalanceWaistbandToLegs_SetsWaistbandWidthToLegWaistSum()
    {
        var front = TestPieceFactory.Rect("Front Leg", 90);
        var back = TestPieceFactory.Rect("Back Leg", 90);
        var wb = new PieceDefinition
        {
            Name = "Waistband",
            Points = [[0, 0], [100, 0], [100, 20], [0, 20]],
        };
        var pieces = new List<PieceDefinition> { front, back, wb };

        PatternAutoRefineService.BalanceWaistbandToLegs(pieces);

        var wbAfter = pieces.First(p => p.Name == "Waistband");
        Assert.True(wbAfter.Points[1][0] > 100);
    }

    [Fact]
    public void DraftProductionPieces_AppliesAutoRefine()
    {
        var sizeChart = new FakeSizeChartService();
        var drafting = new PatternDraftingService(sizeChart, new FakeBlockGenerator());

        var pieces = drafting.DraftProductionPieces("slim", "M");

        Assert.Contains(pieces, p => p.Name == "Front Leg");
        Assert.Contains(pieces, p => p.Name == "Waistband");
        Assert.All(pieces.Where(p => p.Name is "Front Leg" or "Back Leg" or "Waistband"),
            p => Assert.True(p.SeamAllowance > 0));
    }

    [Fact]
    public void DraftProductionPieces_Waistband_HasCurvedAttachEdge()
    {
        var drafting = new PatternDraftingService(new FakeSizeChartService(), new FakeBlockGenerator());
        var wb = drafting.DraftProductionPieces("slim", "M").First(p => p.Name == "Waistband");

        Assert.True(wb.Points.Count >= 7, "Waistband should have a multi-point curved attach edge.");
        Assert.True(wb.Points[2][1] > wb.Points[0][1], "Center attach point should dip below side corners.");
        Assert.Equal(wb.Points[4][0], wb.Points[5][0]);
        Assert.Equal(wb.Points[0][0], wb.Points[6][0]);
    }

    [Fact]
    public void DraftProductionPieces_FrontLeg_HasCurvedWaistEdge()
    {
        var drafting = new PatternDraftingService(new FakeSizeChartService(), new FakeBlockGenerator());
        var front = drafting.DraftProductionPieces("slim", "M").First(p => p.Name == "Front Leg");

        Assert.True(front.Points.Count >= 13, "Front leg should include multi-point curved waist.");
        Assert.True(front.Points[2][1] > front.Points[0][1], "Waist center should dip below side corners.");
        Assert.Single(front.Notches!.Where(n => n[1] == front.Points[2][1] && n[0] == front.Points[2][0]));
    }

    [Fact]
    public void BalanceWaistbandToLegs_KeepsRightEdgeVerticalOnCurvedBand()
    {
        var front = TestPieceFactory.Rect("Front Leg", 120);
        var back = TestPieceFactory.Rect("Back Leg", 120);
        var wb = new PieceDefinition
        {
            Name = "Waistband",
            Points =
            [
                [0, 0], [30, 2], [60, 3], [90, 2], [120, 0], [120, 20], [0, 20],
            ],
        };
        PatternAutoRefineService.BalanceWaistbandToLegs([front, back, wb]);

        Assert.Equal(wb.Points[4][0], wb.Points[5][0]);
    }

    private sealed class FakeBlockGenerator : IBlockGeneratorService
    {
        public BlockDefinition GetDefinition(string styleKey) => new();
        public Dictionary<string, decimal> GetEffectiveEase(string styleKey) =>
            new(StringComparer.OrdinalIgnoreCase);
        public void SetEaseOverride(string styleKey, string key, decimal value) { }
        public void ResetEase(string styleKey) { }
        public GeneratedBlock GenerateBlock(string styleKey) => new();
    }

    private sealed class FakeSizeChartService : ISizeChartService
    {
        private readonly List<SizeRow> _rows =
        [
            new() { MeasurementPoint = "Waist", Values = [84m, 88m, 92m, 96m, 100m, 104m] },
            new() { MeasurementPoint = "Hip", Values = [100m, 104m, 108m, 112m, 116m, 120m] },
            new() { MeasurementPoint = "Front Rise", Values = [27m, 28m, 29m, 30m, 31m, 32m] },
            new() { MeasurementPoint = "Back Rise", Values = [39m, 40m, 41m, 42m, 43m, 44m] },
            new() { MeasurementPoint = "Thigh", Values = [60m, 63m, 66m, 69m, 72m, 75m] },
            new() { MeasurementPoint = "Knee", Values = [40m, 42m, 44m, 46m, 48m, 50m] },
            new() { MeasurementPoint = "Ankle", Values = [36m, 38m, 40m, 42m, 44m, 46m] },
            new() { MeasurementPoint = "Inseam", Values = [80m, 80m, 80m, 80m, 80m, 80m] },
        ];

        public SizeChartSnapshot GetSnapshot(int? patternId = null) => new()
        {
            Columns = GetColumnLabels(patternId),
            Rows = GetAll(patternId),
        };

        public IReadOnlyList<string> GetColumnLabels(int? patternId = null) => ["XS", "S", "M", "L", "XL", "XXL"];
        public IReadOnlyList<SizeRow> GetAll(int? patternId = null) => _rows;
        public string ExportCsv(int? patternId = null) => string.Empty;
        public (bool Ok, string? Error) TryAddSizeColumn(string label, int? patternId = null) => (false, null);
        public (bool Ok, string? Error) TryAddMeasurementRow(string measurementPoint, string? copyFromMeasurementPoint, int? patternId = null) => (false, null);
        public (bool Ok, string? Error) TryDeleteMeasurementRow(string measurementPoint, int? patternId = null) => (true, null);
        public (bool Ok, string? Error) TryDeleteSizeColumn(int columnIndex, int? patternId = null) => (true, null);
        public (bool Ok, string? Error) TryUpdateCell(string measurementPoint, int columnIndex, decimal value, int? patternId = null) => (true, null);
        public (bool Ok, string? Error) TryUpdateRowMeta(string measurementPoint, decimal toleranceCm, string? measurementMethod, int? patternId = null) => (true, null);
        public (bool Ok, string? Error) SetChartSettings(int patternId, bool useCustomChart, string chartMode) => (true, null);
        public (bool Ok, string? Error) CopyGlobalToPattern(int patternId) => (true, null);
        public (bool Ok, string? Error) InitializeGarmentTemplate(int patternId) => (true, null);
        public IReadOnlyList<MeasurementProfile> GetMeasurementProfiles() => [];
        public (bool Ok, string? Error) SaveMeasurementProfile(string name, IReadOnlyDictionary<string, decimal> measurements) => (true, null);
    }
}
