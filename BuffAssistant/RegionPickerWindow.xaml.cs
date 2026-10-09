using System;
using System.Windows;
using System.Windows.Input;
using BuffAssistant.Models;
using System.Windows.Controls;

namespace BuffAssistant;
public partial class RegionPickerWindow : Window
{
    private Point _start;
    private bool _dragging;
    public ScreenRect? SelectedRect { get; private set; }
    public RegionPickerWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }
    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RootCanvas); _dragging = true;
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _start.X); Canvas.SetTop(SelectionRect, _start.Y);
        SelectionRect.Width = 0; SelectionRect.Height = 0; RootCanvas.CaptureMouse();
    }
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var current = e.GetPosition(RootCanvas);
        Canvas.SetLeft(SelectionRect, Math.Min(_start.X, current.X));
        Canvas.SetTop(SelectionRect, Math.Min(_start.Y, current.Y));
        SelectionRect.Width = Math.Abs(current.X - _start.X);
        SelectionRect.Height = Math.Abs(current.Y - _start.Y);
    }
    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false; RootCanvas.ReleaseMouseCapture();
        var current = e.GetPosition(RootCanvas);
        var left = Math.Min(_start.X, current.X); var top = Math.Min(_start.Y, current.Y);
        var width = Math.Abs(current.X - _start.X); var height = Math.Abs(current.Y - _start.Y);
        if (width < 5 || height < 5) { DialogResult = false; Close(); return; }
        var screenStart = PointToScreen(new Point(left, top));
        var screenEnd = PointToScreen(new Point(left + width, top + height));
        SelectedRect = new ScreenRect((int)Math.Round(screenStart.X), (int)Math.Round(screenStart.Y),
            (int)Math.Round(screenEnd.X - screenStart.X), (int)Math.Round(screenEnd.Y - screenStart.Y));
        DialogResult = true; Close();
    }
}
