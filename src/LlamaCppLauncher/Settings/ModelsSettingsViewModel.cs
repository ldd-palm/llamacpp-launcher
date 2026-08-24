using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Settings;

public sealed partial class ModelsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ModelProfile> Models { get; }

    [ObservableProperty]
    private ModelProfile? _selectedModel;

    public ModelsSettingsViewModel(IEnumerable<ModelProfile> models)
    {
        Models = new ObservableCollection<ModelProfile>(models);
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

        // ModelProfile is a plain data class (no INotifyPropertyChanged) so bound fields like
        // "is this the default model" won't refresh on their own — force one now.
        OnPropertyChanged(nameof(SelectedModel));
    }

    public void ApplyTo(AppConfig config)
    {
        config.Models = Models.ToList();
    }
}
