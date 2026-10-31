using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Shared.Catalogs;
using Kuroe.Tests.Support;
using Xunit;

namespace Kuroe.Tests.Catalogs;

/// <summary>模型选择与注销的联动：选择要求已注册，注销当前模型即取消选择。</summary>
public sealed class ModelServiceTests
{
    [Fact]
    public void Selecting_an_unregistered_model_is_rejected()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<ModelSelection> selected = fixture.Models.Select(ModelName.Create("nope").Value);

        Assert.True(selected.IsError);
        Assert.Equal("fake", fixture.Models.Current?.Value);
    }

    [Fact]
    public void Unselect_without_a_selection_changes_nothing()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();
        Assert.False(fixture.Models.Unselect().IsError);

        ErrorOr<ModelSelection> again = fixture.Models.Unselect();

        Assert.False(again.IsError);
        Assert.False(again.Value.Changed);
    }

    [Fact]
    public void Removing_the_current_model_cancels_the_selection()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<ModelRemoval> removal = fixture.Models.Remove(ModelName.Create("fake").Value);

        Assert.False(removal.IsError);
        Assert.Null(fixture.Models.Current);
        Assert.Empty(removal.Value.Failures);
        Assert.DoesNotContain(fixture.Catalog.Snapshot().Models,
            model => model.ModelName.Value == "fake");
    }

    [Fact]
    public void Removing_an_unselected_model_keeps_the_selection()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();
        Assert.False(fixture.Catalog.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value).IsError);
        Assert.False(fixture.Models.Select(ModelName.Create("m2").Value).IsError);

        ErrorOr<ModelRemoval> removal = fixture.Models.Remove(ModelName.Create("fake").Value);

        Assert.False(removal.IsError);
        Assert.Equal("m2", fixture.Models.Current?.Value);
        Assert.Empty(removal.Value.Failures);
    }
}
