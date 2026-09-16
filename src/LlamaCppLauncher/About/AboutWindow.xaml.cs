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
        OpenUrl(viewModel.ProjectHomepageUrl);
    }

    private void OnWebChatUrlClick(object sender, RoutedEventArgs e)
    {
        var viewModel = (AboutViewModel)DataContext;
        OpenUrl(viewModel.WebChatUrl);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Best-effort — nothing sensible to surface from here if there's no default handler.
        }
    }
}
