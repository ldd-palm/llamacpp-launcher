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

    [Fact]
    public void HasNoModels_IsTrue_WhenConstructedEmpty()
    {
        var viewModel = new ModelsSettingsViewModel(Array.Empty<ModelProfile>());

        Assert.True(viewModel.HasNoModels);
    }

    [Fact]
    public void HasNoModels_IsFalse_WhenModelsPresent()
    {
        var viewModel = new ModelsSettingsViewModel(new[] { Model("a.gguf") });

        Assert.False(viewModel.HasNoModels);
    }

    [Fact]
    public void RefreshFromDirectory_PopulatesFromDiscoveredFiles_AndPreservesExistingEdits()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.gguf"), "");
            File.WriteAllText(Path.Combine(tempDir, "b.gguf"), "");

            ModelProfile existingA = Model("a.gguf");
            existingA.Alias = "custom-alias";
            var viewModel = new ModelsSettingsViewModel(new[] { existingA });

            viewModel.RefreshFromDirectory(tempDir);

            Assert.Equal(2, viewModel.Models.Count);
            Assert.False(viewModel.HasNoModels);
            ModelProfile refreshedA = viewModel.Models.Single(m => m.FileName == "a.gguf");
            Assert.Equal("custom-alias", refreshedA.Alias);
            Assert.Contains(viewModel.Models, m => m.FileName == "b.gguf");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void RefreshFromDirectory_ResultsInEmptyList_WhenDirectoryHasNoGgufFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var viewModel = new ModelsSettingsViewModel(new[] { Model("a.gguf") });

            viewModel.RefreshFromDirectory(tempDir);

            Assert.Empty(viewModel.Models);
            Assert.True(viewModel.HasNoModels);
            Assert.Null(viewModel.SelectedModel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
