using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 存档备份：把实例的 saves 复制到 saves-backup\时间戳，只留最近 N 份。
/// </summary>
public class WindowSaveBackup : Window
{
    private const int KeepCount = 5;

    private readonly ComboBox _instances = new();
    private readonly ListBox _saves = new();
    private readonly ListBox _backups = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _log = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowSaveBackup()
    {
        Title = "存档备份 · 昔涟";
        Width = 720;
        Height = 640;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "先存一份，再放心折腾",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "备份会复制到实例目录下的 saves-backup\\时间戳\\，只保留最近 5 份。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(new TextBlock { Text = "实例：", VerticalAlignment = VerticalAlignment.Center });
        _instances.Width = 280;
        _instances.Height = 28;
        _instances.Margin = new Thickness(0, 0, 10, 0);
        _instances.SelectionChanged += (_, _) => Refresh();
        top.Children.Add(_instances);
        var scan = new Button { Content = "刷新", Width = 80, Height = 28 };
        scan.Click += (_, _) => Refresh();
        top.Children.Add(scan);
        panel.Children.Add(top);

        panel.Children.Add(new TextBlock { Text = "存档", Foreground = new SolidColorBrush(Color.FromRgb(150, 90, 120)) });
        _saves.Height = 150;
        _saves.BorderThickness = new Thickness(1);
        _saves.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        _saves.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        panel.Children.Add(_saves);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
        var backup = new Button { Content = "备份全部存档", Width = 140, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
        backup.Click += (_, _) => Backup();
        var open = new Button { Content = "打开存档目录", Width = 140, Height = 32 };
        open.Click += (_, _) => OpenSaves();
        actions.Children.Add(backup);
        actions.Children.Add(open);
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock { Text = "已有备份（最早在下）", Foreground = new SolidColorBrush(Color.FromRgb(150, 90, 120)) });
        _backups.Height = 130;
        _backups.BorderThickness = new Thickness(1);
        _backups.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        _backups.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        panel.Children.Add(_backups);

        var restore = new Button { Content = "恢复到选中的备份", Width = 180, Height = 32, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 8) };
        restore.Click += (_, _) => Restore();
        panel.Children.Add(restore);

        _status.Text = "选一个实例。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.Height = 110;
        _log.Margin = new Thickness(0, 8, 0, 0);
        _log.FontFamily = new FontFamily("Consolas");
        _log.FontSize = 11;
        _log.Background = new SolidColorBrush(Color.FromRgb(250, 240, 246));
        panel.Children.Add(_log);

        Content = panel;

        Loaded += (_, _) =>
        {
            var vdir = Path.Combine(McRoot, "versions");
            if (!Directory.Exists(vdir)) return;
            foreach (var d in Directory.GetDirectories(vdir).OrderBy(Path.GetFileName))
                _instances.Items.Add(Path.GetFileName(d));
            if (_instances.Items.Count > 0) _instances.SelectedIndex = 0;
        };
    }

    private string? InstDir
    {
        get
        {
            var inst = _instances.SelectedItem?.ToString();
            return string.IsNullOrEmpty(inst) ? null : Path.Combine(McRoot, "versions", inst);
        }
    }

    private void Log(string s)
    {
        _log.AppendText(s + Environment.NewLine);
        _log.ScrollToEnd();
    }

    private void Refresh()
    {
        _saves.Items.Clear();
        _backups.Items.Clear();
        var dir = InstDir;
        if (dir is null) return;

        var saves = Path.Combine(dir, "saves");
        if (Directory.Exists(saves))
        {
            foreach (var w in Directory.GetDirectories(saves))
            {
                var mb = Math.Round(new DirectoryInfo(w).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024.0 / 1024.0, 1);
                _saves.Items.Add($"{Path.GetFileName(w)}　（{mb} MB）");
            }
        }

        var bak = Path.Combine(dir, "saves-backup");
        if (Directory.Exists(bak))
        {
            foreach (var b in Directory.GetDirectories(bak).OrderBy(Path.GetFileName))
                _backups.Items.Add(Path.GetFileName(b));
        }

        _status.Text = $"存档 {_saves.Items.Count} 个，备份 {_backups.Items.Count} 份。";
    }

    private void Backup()
    {
        var dir = InstDir;
        if (dir is null) return;
        var saves = Path.Combine(dir, "saves");
        if (!Directory.Exists(saves) || Directory.GetDirectories(saves).Length == 0)
        {
            _status.Text = "这个实例没有存档。";
            return;
        }

        var target = Path.Combine(dir, "saves-backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        try
        {
            _status.Text = "正在备份……";
            CopyDir(saves, target);
            Log("已备份到 " + target);

            // 只留最近 N 份
            var bak = Path.Combine(dir, "saves-backup");
            var all = Directory.GetDirectories(bak).OrderBy(Path.GetFileName).ToList();
            while (all.Count > KeepCount)
            {
                Directory.Delete(all[0], true);
                Log("清理旧备份：" + Path.GetFileName(all[0]));
                all.RemoveAt(0);
            }

            Refresh();
            _status.Text = "备份好了。";
        }
        catch (Exception ex)
        {
            _status.Text = "备份失败：" + ex.Message;
        }
    }

    private void Restore()
    {
        var dir = InstDir;
        var pick = _backups.SelectedItem?.ToString();
        if (dir is null || string.IsNullOrEmpty(pick)) { _status.Text = "先在下面选一份备份。"; return; }

        try
        {
            var src = Path.Combine(dir, "saves-backup", pick);
            var dst = Path.Combine(dir, "saves");
            _status.Text = "正在恢复……";
            CopyDir(src, dst);
            Log("已恢复：" + pick);
            Refresh();
            _status.Text = "恢复完成。进游戏看看。";
        }
        catch (Exception ex)
        {
            _status.Text = "恢复失败：" + ex.Message;
        }
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, f);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target, true);
        }
    }

    private void OpenSaves()
    {
        var dir = InstDir;
        if (dir is null) return;
        var saves = Path.Combine(dir, "saves");
        Directory.CreateDirectory(saves);
        try { Process.Start(new ProcessStartInfo(saves) { UseShellExecute = true }); } catch { }
    }
}
