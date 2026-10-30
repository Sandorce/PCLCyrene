using PCL.Cyrene;
using System;
using PCL.Cyrene;
using System.Windows;
using PCL.Cyrene;
using System.Windows.Controls;
using PCL.Cyrene;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 更新历史：把所有版本的改动列出来，可滚动。
/// </summary>
public class WindowHistory : Window
{
    public WindowHistory()
    {
        Title = "更新历史 · 昔涟";
        Width = 640;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };

        var title = new TextBlock
        {
            Text = "CCL 更新历史",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var close = new Button
        {
            Content = "关闭",
            Width = 96,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);

        var box = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(252, 244, 249)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225)),
            Text = ChangeLog.AllNotes()
        };
        panel.Children.Add(box);

        Content = panel;
    }
}
