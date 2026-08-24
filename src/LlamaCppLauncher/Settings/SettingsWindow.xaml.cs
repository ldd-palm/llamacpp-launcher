// src/LlamaCppLauncher/Settings/SettingsWindow.xaml.cs
using Wpf.Ui.Controls;

namespace LlamaCppLauncher.Settings;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
