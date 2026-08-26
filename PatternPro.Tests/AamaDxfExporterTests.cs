using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using Moq;
using Xunit;

namespace PatternPro.Tests;

public class AamaDxfExporterTests
{
    private static string ExtractDxfFromDraftZip(byte[] zipBytes, string entryName)
    {
        using var ms = new MemoryStream(zipBytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = zip.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing {entryName}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static ExportService CreateDraftExportService()
    {
        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = TestPieceFactory.MinimalFactorySet().ToList(),
            });

        return new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            Mock.Of<IProductionCertificationService>());
    }

    [Fact]
    public void DxfExport_ContainsAamaStructure()
    {
        var sut = CreateDraftExportService();
        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);
        var dxf = ExtractDxfFromDraftZip(bytes, "skinny_M.dxf");

        Assert.Contains("BLOCKS", dxf);
        Assert.Contains("INSERT", dxf);
        Assert.Contains("$INSUNITS", dxf);
        Assert.Contains("AC1009", dxf);
        Assert.True(dxf.Length > 1024);
    }

    [Fact]
    public void DxfExport_HasClosedPolylineOnLayer1()
    {
        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = [TestPieceFactory.Rect("Front Leg", seamAllowance: 1.0)],
            });

        var sut = new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            Mock.Of<IProductionCertificationService>());

        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);
        var dxf = ExtractDxfFromDraftZip(bytes, "skinny_M.dxf");

        Assert.Matches("(?m)^POLYLINE\r?\n8\r?\n1", dxf);
        Assert.Contains("70\r\n1", dxf);
        Assert.Matches("(?m)^POLYLINE\r?\n8\r?\n14", dxf);
    }

    [Fact]
    public void DxfExport_NamesBlocksWithPieceAndSize()
    {
        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = [TestPieceFactory.Rect("Front Leg")],
            });

        var sut = new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            Mock.Of<IProductionCertificationService>());

        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);
        var dxf = ExtractDxfFromDraftZip(bytes, "skinny_M.dxf");

        Assert.Contains("2\r\nFront_Leg_M", dxf);
    }

    [Fact]
    public void DxfExport_DoesNotUseLegacyCutLayer()
    {
        var sut = CreateDraftExportService();
        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);
        var dxf = ExtractDxfFromDraftZip(bytes, "skinny_M.dxf");

        Assert.DoesNotContain("8\r\nCUT", dxf);
    }

    [Fact]
    public void DxfExport_UsesCentimeterUnits()
    {
        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int?>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = [TestPieceFactory.Rect("Front Leg", width: 120, seamAllowance: 1.0)],
            });

        var sut = new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            Mock.Of<IProductionCertificationService>());

        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);
        var dxf = ExtractDxfFromDraftZip(bytes, "skinny_M.dxf");

        Assert.Contains("70\r\n5", dxf); // $INSUNITS = 5 (centimeters)
        Assert.DoesNotContain("70\r\n4", dxf); // not millimeters
        // 120 px = 40 cm — should not appear as ~400 (mm legacy scale)
        Assert.Contains("40", dxf);
        Assert.DoesNotMatch("(?m)^10\r\n400", dxf);
    }
}
