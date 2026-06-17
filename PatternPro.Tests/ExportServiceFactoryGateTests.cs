using Moq;
using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using Xunit;

namespace PatternPro.Tests;

public class ExportServiceFactoryGateTests
{
    [Fact]
    public void BuildExportPackage_Factory_WhenNotCertified_Throws()
    {
        var certification = new Mock<IProductionCertificationService>();
        certification.Setup(c => c.ValidateForFactory(1, "skinny"))
            .Returns(new ProductionValidationReport
            {
                PatternId = 1,
                CanExportToFactory = false,
                Issues = [new ProductionValidationIssue { Code = "NOT_APPROVED", Message = "Not approved" }],
            });

        var sut = new ExportService(
            Mock.Of<IPatternDraftingService>(),
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            certification.Object);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 1, ExportPurpose.Factory));

        Assert.Contains("Factory export blocked", ex.Message);
    }

    [Fact]
    public void BuildExportPackage_Draft_SkipsCertification()
    {
        var certification = new Mock<IProductionCertificationService>();
        certification.Setup(c => c.ValidateForFactory(It.IsAny<int>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Should not be called"));

        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = TestPieceFactory.MinimalFactorySet().ToList(),
            });

        var sut = new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            certification.Object);

        var (bytes, _, _) = sut.BuildExportPackage("skinny", "DXF", ["M"], patternId: 0, ExportPurpose.Draft);

        Assert.True(bytes.Length > 0);
        certification.Verify(c => c.ValidateForFactory(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildExportPackage_Draft_Pdf_IncludesPdfBytes()
    {
        var drafting = new Mock<IPatternDraftingService>();
        drafting.Setup(d => d.DraftGradedSet("skinny", It.IsAny<IReadOnlyList<string>>()))
            .Returns(new Dictionary<string, IReadOnlyList<PieceDefinition>>
            {
                ["M"] = TestPieceFactory.MinimalFactorySet().ToList(),
            });

        var sut = new ExportService(
            drafting.Object,
            Mock.Of<IPieceService>(),
            Mock.Of<IPatternService>(),
            Mock.Of<IProductionCertificationService>());

        var (bytes, contentType, fileName) = sut.BuildExportPackage("skinny", "PDF", ["M"], patternId: 0, ExportPurpose.Draft);

        Assert.True(bytes.Length > 100);
        Assert.Equal("application/zip", contentType);
        Assert.Contains("pdf", fileName);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
