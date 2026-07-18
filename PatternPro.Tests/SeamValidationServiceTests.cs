using PatternPro.Business.Services;
using Xunit;

namespace PatternPro.Tests;

public class SeamValidationServiceTests
{
    private readonly SeamValidationService _sut = new();

    [Fact]
    public void ValidatePieces_EmptyList_ReturnsNoPiecesError()
    {
        var (errors, _) = _sut.ValidatePieces([], "skinny");
        Assert.Contains(errors, e => e.Code == "NO_PIECES");
    }

    [Fact]
    public void ValidatePieces_MissingRequiredPiece_ReturnsMissingPieceError()
    {
        var (errors, _) = _sut.ValidatePieces([TestPieceFactory.Rect("Front Leg")], "skinny");
        Assert.Contains(errors, e => e.Code == "MISSING_PIECE");
    }

    [Fact]
    public void ValidatePieces_MinimalValidSet_ReturnsNoErrors()
    {
        var (errors, warnings) = _sut.ValidatePieces(TestPieceFactory.MinimalFactorySet(), "skinny");
        Assert.Empty(errors);
        Assert.NotNull(warnings);
    }
}
