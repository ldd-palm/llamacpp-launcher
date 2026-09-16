// src/LlamaCppLauncher/Settings/SettingsWindow.xaml.cs
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace LlamaCppLauncher.Settings;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnParameterReferenceClick(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "model-parameters.txt");
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Best-effort convenience link — nothing sensible to surface from here if there's no
            // default handler for .txt files or the file is missing from the publish output.
        }
    }
}
