using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 内置资源库：把光影 / 资源包 / 整合包列出来，选个实例，一键部署。
/// </summary>
public class WindowResourceLibrary : Window
{
    private static readonly (string Folder, string Ext, string Title)[] Categories =
    [
        ("光影包", "*.zip", "光影包"),
        ("资源包", "*.zip", "资源包"),
        ("整合包", "*.mrpack", "整合包")
    ];

    private readonly ListBox _list = new();
    private readonly ComboBox _instances = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _log = new();
    private readonly Button _deploy = new();
    private readonly List<Item> _items = new();

    private static string Root => @"I:\CCL\资源库";
    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    private sealed class Item
    {
        public string Category = "";
        public string Path = "";
        public string Display = "";
    }

    public WindowResourceLibrary()
    {
        Title = "资源库 · 昔涟";
        Width = 700;
        Height = 600;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "东西都在这儿，点一下就好",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "把光影 / 资源包 / .mrpack 放进 I:\\CCL\\资源库 对应的文件夹，这里就会列出来。选一个实例，然后点部署。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(new TextBlock { Text = "目标实例：", VerticalAlignment = VerticalAlignment.Center });
        _instances.Width = 260;
        _instances.Height = 28;
        _instances.Margin = new Thickness(0, 0, 10, 0);
        top.Children.Add(_instances);
        var refresh = new Button { Content = "刷新", Width = 80, Height = 28 };
        refresh.Click += (_, _) => Scan();
        top.Children.Add(refresh);
        panel.Children.Add(top);

        _list.Height = 260;
        _list.BorderThickness = new Thickness(1);
        _list.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        _list.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        panel.Children.Add(_list);

        _deploy.Content = "部署到选中实例";
        _deploy.Height = 34;
        _deploy.Width = 170;
        _deploy.HorizontalAlignment = HorizontalAlignment.Left;
        _deploy.Margin = new Thickness(0, 12, 0, 8);
        _deploy.Click += (_, _) => Deploy();
        panel.Children.Add(_deploy);

        _status.Text = "还没开始。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.Height = 120;
        _log.Margin = new Thickness(0, 8, 0, 0);
        _log.FontFamily = new FontFamily("Consolas");
        _log.FontSize = 11;
        _log.Background = new SolidColorBrush(Color.FromRgb(250, 240, 246));
        panel.Children.Add(_log);

        Content = panel;
        Loaded += (_, _) => Scan();
    }

    private void Scan()
    {
        _items.Clear();
        _list.Items.Clear();
        _log.Clear();

        Directory.CreateDirectory(Root);
        foreach (var (folder, ext, title) in Categories)
        {
            var dir = Path.Combine(Root, folder);
            Directory.CreateDirectory(dir);
            foreach (var file in Directory.GetFiles(dir, ext))
            {
                var mb = Math.Round(new FileInfo(file).Length / 1024.0 / 1024.0, 1);
                var item = new Item
                {
                    Category = title,
                    Path = file,
                    Display = $"[{title}] {Path.GetFileName(file)}　（{mb} MB）"
                };
                _items.Add(item);
                _list.Items.Add(item.Display);
            }
        }

        // 本机 I:\CCL 根目录下的 .mrpack 也列进来
        try
        {
            foreach (var file in Directory.GetFiles(@"I:\CCL", "*.mrpack"))
            {
                var mb = Math.Round(new FileInfo(file).Length / 1024.0 / 1024.0, 1);
                var item = new Item
                {
                    Category = "整合包",
                    Path = file,
                    Display = $"[本机整合包] {Path.GetFileName(file)}　（{mb} MB）"
                };
                _items.Add(item);
                _list.Items.Add(item.Display);
            }
        }
        catch { }

        _instances.Items.Clear();
        var vdir = Path.Combine(McRoot, "versions");
        if (Directory.Exists(vdir))
        {
            foreach (var d in Directory.GetDirectories(vdir).OrderBy(Path.GetFileName))
                _instances.Items.Add(Path.GetFileName(d));
        }

        if (_instances.Items.Count > 0) _instances.SelectedIndex = 0;
        _status.Text = _items.Count == 0
            ? "资源库是空的。把文件放进 I:\\CCL\\资源库 的对应子文件夹，再点「刷新」。"
            : $"共 {_items.Count} 个资源，{_instances.Items.Count} 个实例。";
    }

    private void Deploy()
    {
        if (_list.SelectedIndex < 0)
        {
            _status.Text = "先在上面选一个资源。";
            return;
        }

        var item = _items[_list.SelectedIndex];
        var instance = _instances.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(instance))
        {
            _status.Text = "没有可用的实例，先把游戏装好。";
            return;
        }

        try
        {
            if (item.Category == "整合包")
            {
                _status.Text = "正在安装整合包……";
                CyreneHost.InstallModpack?.Invoke(item.Path);
                _log.AppendText("已提交安装：" + item.Path + Environment.NewLine);
                return;
            }

            var target = Path.Combine(McRoot, "versions", instance,
                item.Category == "光影包" ? "shaderpacks" : "resourcepacks");
            Directory.CreateDirectory(target);
            var dest = Path.Combine(target, Path.GetFileName(item.Path));
            File.Copy(item.Path, dest, true);

            _status.Text = "放好了：" + dest;
            _log.AppendText($"已复制到 {item.Category} → {instance}" + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _status.Text = "部署失败：" + ex.Message;
        }
    }
}
