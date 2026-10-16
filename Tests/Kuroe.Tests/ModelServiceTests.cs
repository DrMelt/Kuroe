using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Shared.Catalogs;
using Xunit;

namespace Kuroe.Tests;

/// <summary>模型选择与注销的联动：选择要求已注册，注销当前模型即取消选择。</summary>
public sealed class ModelServiceTests
{
    [Fact]
    public void Selecting_an_unregistered_model_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<ModelSelection> selected = environment.Models.Select(ModelName.Create("nope").Value);

        Assert.True(selected.IsError);
        Assert.Equal("fake", environment.Models.Current?.Value);
    }

    [Fact]
    public void Unselect_without_a_selection_changes_nothing()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Models.Unselect().ThrowIfError();

        ModelSelection again = environment.Models.Unselect().ThrowIfError();

        Assert.False(again.Changed);
    }

    [Fact]
    public void Removing_the_current_model_cancels_the_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ModelRemoval removal = environment.Models.Remove(ModelName.Create("fake").Value).ThrowIfError();

        Assert.Null(environment.Models.Current);
        Assert.Empty(removal.Failures);
        Assert.DoesNotContain(environment.Catalog.Snapshot().Models,
            model => model.ModelName.Value == "fake");
    }

    [Fact]
    public void Removing_an_unselected_model_keeps_the_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Catalog.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value).ThrowIfError();
        environment.Models.Select(ModelName.Create("m2").Value).ThrowIfError();

        ModelRemoval removal = environment.Models.Remove(ModelName.Create("fake").Value).ThrowIfError();

        Assert.Equal("m2", environment.Models.Current?.Value);
        Assert.Empty(removal.Failures);
    }
}
