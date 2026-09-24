using ErrorOr;
using Kuroe.Shared.Catalogs;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>模型选择与注销的联动：选择要求已注册，注销当前模型即取消选择。</summary>
public sealed class ModelServiceTests
{
    [Fact]
    public void Selecting_an_unregistered_model_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<ModelSelection> selected = harness.Models.Select("nope");

        Assert.True(selected.IsError);
        Assert.Equal("fake", harness.Models.Current);
    }

    [Fact]
    public void Unselect_without_a_selection_changes_nothing()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Models.Unselect().ThrowIfError();

        ModelSelection again = harness.Models.Unselect().ThrowIfError();

        Assert.False(again.Changed);
    }

    [Fact]
    public void Removing_the_current_model_cancels_the_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ModelRemoval removal = harness.Models.Remove("fake").ThrowIfError();

        Assert.Null(harness.Models.Current);
        Assert.Empty(removal.Failures);
        Assert.DoesNotContain(harness.Catalog.Snapshot().Models,
            model => model.ModelName.Value == "fake");
    }

    [Fact]
    public void Removing_an_unselected_model_keeps_the_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Catalog.AddModel("m2", "test").ThrowIfError();
        harness.Models.Select("m2").ThrowIfError();

        ModelRemoval removal = harness.Models.Remove("fake").ThrowIfError();

        Assert.Equal("m2", harness.Models.Current);
        Assert.Empty(removal.Failures);
    }
}