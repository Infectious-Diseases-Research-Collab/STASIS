using STASIS.Models;
using STASIS.Pages.Boxes;
using Xunit;

namespace STASIS.Tests.Pages;

// Layout helpers only -- they never touch the injected services, so null! is safe here.
public class BoxSearchLayoutTests
{
    private readonly SearchModel _model = new(null!, null!);

    private static Box Binder(params int?[] rows) => new()
    {
        BoxLabel = "FP1",
        BoxType = "Filter Paper Binder",
        Specimens = rows.Select(r => new Specimen { BarcodeID = $"FP{r}", PositionRow = r }).ToList()
    };

    [Fact]
    public void FilterPaperBinder_IsLinear_GridBoxesAreNot()
    {
        Assert.True(_model.IsLinear(Binder()));
        Assert.False(_model.IsLinear(new Box { BoxType = "81-slot" }));
        Assert.False(_model.IsLinear(new Box { BoxType = "100-slot" }));
    }

    [Fact]
    public void EmptyBinder_HasOneHundredSlots()
    {
        var box = Binder();

        Assert.Equal(100, _model.GetLinearSlotCount(box));
        Assert.Equal(100, _model.GetCapacity(box));
    }

    [Fact]
    public void LegacyBinderPastOneHundred_GrowsToHighestRow()
    {
        var box = Binder(1, 50, 198);

        Assert.Equal(198, _model.GetLinearSlotCount(box));
        Assert.Equal(198, _model.GetCapacity(box));
    }

    [Fact]
    public void GridBoxCapacity_IsGridSizeSquared()
    {
        Assert.Equal(81, _model.GetCapacity(new Box { BoxType = "81-slot" }));
        Assert.Equal(100, _model.GetCapacity(new Box { BoxType = "100-slot" }));
    }
}
