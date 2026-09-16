using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;

namespace LlamaCppLauncher.Settings;

public sealed partial class ModelsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ModelProfile> Models { get; }

    [ObservableProperty]
    private ModelProfile? _selectedModel;

    public bool HasNoModels => Models.Count == 0;

    public ModelsSettingsViewModel(IEnumerable<ModelProfile> models)
    {
        Models = new ObservableCollection<ModelProfile>(models);
        Models.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoModels));
        SelectedModel = Models.FirstOrDefault();
    }

    [RelayCommand]
    public void SetSelectedModelAsDefault()
    {
        if (SelectedModel is null)
        {
            return;
        }

        foreach (ModelProfile model in Models)
        {
            model.IsDefault = ReferenceEquals(model, SelectedModel);
        }
    }

    /// Re-scans <paramref name="modelsDirectory"/> for .gguf files and rebuilds the Models list,
    /// preserving already-edited settings for files that are still present.
    public void RefreshFromDirectory(string modelsDirectory)
    {
        IReadOnlyList<string> discovered = ModelDiscoveryService.DiscoverModelFiles(modelsDirectory);
        List<ModelProfile> merged = ModelDiscoveryService.MergeWithConfiguredModels(discovered, Models.ToList());

        string? previouslySelectedFileName = SelectedModel?.FileName;

        Models.Clear();
        foreach (ModelProfile model in merged)
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault(m => string.Equals(m.FileName, previouslySelectedFileName, StringComparison.OrdinalIgnoreCase))
            ?? Models.FirstOrDefault();
    }

    public void ApplyTo(AppConfig config)
    {
        config.Models = Models.ToList();
    }
}
