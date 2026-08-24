// src/LlamaCppLauncher/About/AboutWindow.xaml.cs
using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace LlamaCppLauncher.About;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnProjectHomepageClick(object sender, RoutedEventArgs e)
    {
        var viewModel = (AboutViewModel)DataContext;
        Process.Start(new ProcessStartInfo(viewModel.ProjectHomepageUrl) { UseShellExecute = true });
    }
}
