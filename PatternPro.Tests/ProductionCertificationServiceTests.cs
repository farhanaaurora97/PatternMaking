using Moq;
using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using Xunit;

namespace PatternPro.Tests;

public class ProductionCertificationServiceTests
{
    [Fact]
    public void ValidateForFactory_WhenNotApproved_ReturnsNotApprovedIssue()
    {
        var pattern = new Pattern.Core.Model.Pattern
        {
            Id = 1,
            Style = "Skinny",
            ApprovedForCutting = false,
            CutterTestPassed = false,
        };
        var sut = CreateSut(pattern, TestPieceFactory.MinimalFactorySet());

        var report = sut.ValidateForFactory(1, "skinny");

        Assert.False(report.CanExportToFactory);
        Assert.Contains(report.Issues, i => i.Code == "NOT_APPROVED");
        Assert.Contains(report.Issues, i => i.Code == "CUTTER_TEST");
    }

    [Fact]
    public void ValidateForFactory_WhenFullyCertified_ReturnsCanExport()
    {
        var pattern = new Pattern.Core.Model.Pattern
        {
            Id = 1,
            Style = "Skinny",
            ApprovedForCutting = true,
            CutterTestPassed = true,
            ApprovedBy = "Designer",
            CutterTestedBy = "Factory",
        };
        var sut = CreateSut(pattern, TestPieceFactory.MinimalFactorySet());

        var report = sut.ValidateForFactory(1, "skinny");

        Assert.True(report.CanExportToFactory);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void ApproveForCutting_WhenQcFails_ReturnsNull()
    {
        var pattern = new Pattern.Core.Model.Pattern { Id = 1, Style = "Skinny" };
        var sut = CreateSut(pattern, [TestPieceFactory.Rect("Front Leg")]);

        var result = sut.ApproveForCutting(1, "Designer");

        Assert.Null(result);
    }

    [Fact]
    public void ApproveForCutting_WhenQcPasses_CallsPatternService()
    {
        var pattern = new Pattern.Core.Model.Pattern { Id = 1, Style = "Skinny" };
        var patternService = new Mock<IPatternService>();
        patternService.Setup(s => s.GetAll()).Returns(new[] { pattern });
        patternService.Setup(s => s.ApproveForCutting(1, "Designer"))
            .Returns(new Pattern.Core.Model.Pattern { Id = 1, ApprovedForCutting = true, ApprovedBy = "Designer" });

        var pieceService = new Mock<IPieceService>();
        pieceService.Setup(s => s.GetPieceDefinitions(1, "skinny")).Returns(TestPieceFactory.MinimalFactorySet());

        var sut = new ProductionCertificationService(
            patternService.Object,
            pieceService.Object,
            new SeamValidationService());

        var result = sut.ApproveForCutting(1, "Designer");

        Assert.NotNull(result);
        Assert.True(result!.ApprovedForCutting);
        patternService.Verify(s => s.ApproveForCutting(1, "Designer"), Times.Once);
    }

    [Fact]
    public void CompleteFactoryCertification_AppliesSaApprovesAndEnablesExport()
    {
        var pattern = new Pattern.Core.Model.Pattern
        {
            Id = 1,
            Style = "Skinny",
            CutterTestPassed = true,
        };
        var pieceList = TestPieceFactory.MinimalFactorySet().Select(ClonePiece).ToList();

        var patternService = new Mock<IPatternService>();
        patternService.Setup(s => s.GetAll()).Returns(() => new[] { pattern });
        patternService.Setup(s => s.ApproveForCutting(1, "Designer"))
            .Callback(() => pattern.ApprovedForCutting = true)
            .Returns(() => pattern);

        var pieceService = new Mock<IPieceService>();
        pieceService.Setup(s => s.GetPieceDefinitions(1, It.IsAny<string>())).Returns(pieceList);
        pieceService.Setup(s => s.ApplyDefaultSeamAllowances(1, It.IsAny<string>(), 1.0))
            .Callback(() =>
            {
                foreach (var p in pieceList.Where(p => p.SeamAllowance <= 0))
                    p.SeamAllowance = 3;
            })
            .Returns(3);

        var sut = new ProductionCertificationService(
            patternService.Object,
            pieceService.Object,
            new SeamValidationService());

        var report = sut.CompleteFactoryCertification(1, "skinny", "Designer");

        Assert.True(report.CanExportToFactory);
        Assert.True(report.ApprovedForCutting);
        Assert.True(report.CutterTestPassed);
    }

    [Fact]
    public void CompleteFactoryCertification_WhenCutterTestPending_ApprovesButStillBlocked()
    {
        var pattern = new Pattern.Core.Model.Pattern
        {
            Id = 1,
            Style = "Skinny",
            CutterTestPassed = false,
        };
        var pieceList = TestPieceFactory.MinimalFactorySet().Select(ClonePiece).ToList();

        var patternService = new Mock<IPatternService>();
        patternService.Setup(s => s.GetAll()).Returns(() => new[] { pattern });
        patternService.Setup(s => s.ApproveForCutting(1, "Designer"))
            .Callback(() => pattern.ApprovedForCutting = true)
            .Returns(() => pattern);

        var pieceService = new Mock<IPieceService>();
        pieceService.Setup(s => s.GetPieceDefinitions(1, It.IsAny<string>())).Returns(pieceList);
        pieceService.Setup(s => s.ApplyDefaultSeamAllowances(1, It.IsAny<string>(), 1.0)).Returns(3);

        var sut = new ProductionCertificationService(
            patternService.Object,
            pieceService.Object,
            new SeamValidationService());

        var report = sut.CompleteFactoryCertification(1, "skinny", "Designer");

        Assert.True(report.ApprovedForCutting);
        Assert.False(report.CutterTestPassed);
        Assert.False(report.CanExportToFactory);
        Assert.Contains(report.Issues, i => i.Code == "CUTTER_TEST");
    }

    private static PieceDefinition ClonePiece(PieceDefinition p) => new()
    {
        Name = p.Name,
        Category = p.Category,
        Points = [.. p.Points],
        Grain = p.Grain is null ? null : [.. p.Grain],
        SeamAllowance = p.SeamAllowance,
    };

    private static ProductionCertificationService CreateSut(
        Pattern.Core.Model.Pattern pattern,
        IReadOnlyList<PieceDefinition> pieces)
    {
        var patternService = new Mock<IPatternService>();
        patternService.Setup(s => s.GetAll()).Returns(new[] { pattern });

        var pieceService = new Mock<IPieceService>();
        pieceService.Setup(s => s.GetPieceDefinitions(pattern.Id, It.IsAny<string>())).Returns(pieces);

        return new ProductionCertificationService(
            patternService.Object,
            pieceService.Object,
            new SeamValidationService());
    }
}
