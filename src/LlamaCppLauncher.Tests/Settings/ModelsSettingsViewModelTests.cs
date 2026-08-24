using LlamaCppLauncher.Config;
using LlamaCppLauncher.Settings;

namespace LlamaCppLauncher.Tests.Settings;

public class ModelsSettingsViewModelTests
{
    private static ModelProfile Model(string fileName, bool isDefault = false)
    {
        ModelProfile profile = ModelProfile.CreateDefault(fileName);
        profile.IsDefault = isDefault;
        return profile;
    }

    [Fact]
    public void Constructor_SelectsFirstModelByDefault()
    {
        ModelProfile modelA = Model("a.gguf");
        ModelProfile modelB = Model("b.gguf");

        var viewModel = new ModelsSettingsViewModel(new[] { modelA, modelB });

        Assert.Same(modelA, viewModel.SelectedModel);
    }

    [Fact]
    public void SetSelectedModelAsDefault_ClearsFlagOnOtherModels()
    {
        ModelProfile modelA = Model("a.gguf", isDefault: true);
        ModelProfile modelB = Model("b.gguf");
        var viewModel = new ModelsSettingsViewModel(new[] { modelA, modelB }) { SelectedModel = modelB };

        viewModel.SetSelectedModelAsDefault();

        Assert.False(modelA.IsDefault);
        Assert.True(modelB.IsDefault);
    }

    [Fact]
    public void ApplyTo_WritesModelsListToConfig()
    {
        ModelProfile modelA = Model("a.gguf");
        var viewModel = new ModelsSettingsViewModel(new[] { modelA });
        var config = new AppConfig();

        viewModel.ApplyTo(config);

        Assert.Single(config.Models);
        Assert.Equal("a.gguf", config.Models[0].FileName);
    }
}
