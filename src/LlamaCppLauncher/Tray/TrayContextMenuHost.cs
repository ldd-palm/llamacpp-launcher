// src/LlamaCppLauncher/Tray/TrayContextMenuHost.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Drawing = System.Drawing;

namespace LlamaCppLauncher.Tray;

/// A WinForms NotifyIcon has no WPF PresentationSource of its own, but a WPF ContextMenu needs a live
/// one to open correctly. This keeps a permanently-alive, invisible, zero-size WPF window around purely
/// to serve as that PlacementTarget, so the tray's right-click menu can be a fully-templated WPF
/// ContextMenu (rounded corners, drop shadow, custom hover) instead of a WinForms ContextMenuStrip.
public sealed class TrayContextMenuHost : IDisposable
{
    private readonly Window _hostWindow;

    public TrayContextMenuHost()
    {
        _hostWindow = new Window
        {
            Width = 0,
            Height = 0,
            Left = -10000,
            Top = -10000,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        _hostWindow.Show();
    }

    public void ShowMenu(ContextMenu menu, Drawing.Point screenPoint)
    {
        System.Windows.Point target = ToDeviceIndependentPoint(screenPoint);

        menu.PlacementTarget = _hostWindow;
        menu.Placement = PlacementMode.Absolute;
        menu.HorizontalOffset = target.X;
        menu.VerticalOffset = target.Y;
        menu.IsOpen = true;
    }

    private System.Windows.Point ToDeviceIndependentPoint(Drawing.Point screenPoint)
    {
        PresentationSource? source = PresentationSource.FromVisual(_hostWindow);
        var devicePoint = new System.Windows.Point(screenPoint.X, screenPoint.Y);
        return source?.CompositionTarget is null ? devicePoint : source.CompositionTarget.TransformFromDevice.Transform(devicePoint);
    }

    public void Dispose() => _hostWindow.Close();
}
