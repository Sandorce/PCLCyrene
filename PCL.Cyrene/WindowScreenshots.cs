using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 截图归档：按实例汇总 screenshots，按哈希去重，可一键归档到统一目录。
/// </summary>
public class WindowScreenshots : Window
{
    private readonly TextBox _out = new();
    private readonly TextBlock _status = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowScreenshots()
    {
        Title = "截图归档 · 昔涟";
        Width = 680;
        Height = 520;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "截图都散在哪儿了？",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "扫所有实例的 screenshots，按内容哈希去掉重复，把结果列出来；可以全部归到 I:\\CCL\\截图。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var scan = new Button { Content = "扫描", Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
        scan.Click += (_, _) => Scan();
        var archive = new Button { Content = "全部归档", Width = 120, Height = 30 };
        archive.Click += (_, _) => Archive();
        buttons.Children.Add(scan);
        buttons.Children.Add(archive);
        panel.Children.Add(buttons);

        _status.Text = "点「扫描」开始。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _out.IsReadOnly = true;
        _out.AcceptsReturn = true;
        _out.TextWrapping = TextWrapping.Wrap;
        _out.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _out.Height = 340;
        _out.Margin = new Thickness(0, 10, 0, 0);
        _out.FontFamily = new FontFamily("Consolas");
        _out.FontSize = 11;
        _out.Padding = new Thickness(8);
        _out.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _out.BorderThickness = new Thickness(1);
        _out.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_out);

        Content = panel;
    }

    private static List<FileInfo> Collect()
    {
        var result = new List<FileInfo>();
        var vdir = Path.Combine(McRoot, "versions");
        if (!Directory.Exists(vdir)) return result;
        foreach (var inst in Directory.GetDirectories(vdir))
        {
            var shots = Path.Combine(inst, "screenshots");
            if (!Directory.Exists(shots)) continue;
            result.AddRange(Directory.GetFiles(shots, "*.png").Select(p => new FileInfo(p)));
            result.AddRange(Directory.GetFiles(shots, "*.jpg").Select(p => new FileInfo(p)));
        }

        return result;
    }

    private void Scan()
    {
        var files = Collect();
        _out.Clear();
        if (files.Count == 0) { _status.Text = "一张截图都没有。"; return; }

        var seen = new Dictionary<string, List<string>>();
        foreach (var f in files)
        {
            try
            {
                using var sha = SHA256.Create();
                using var fs = f.OpenRead();
                var h = Convert.ToHexString(sha.ComputeHash(fs)).Substring(0, 16);
                if (!seen.TryGetValue(h, out var list)) seen[h] = list = new List<string>();
                list.Add(f.FullName);
            }
            catch { }
        }

        var dupGroups = seen.Where(p => p.Value.Count > 1).ToList();
        var dupCount = dupGroups.Sum(p => p.Value.Count - 1);

        _out.AppendText($"共 {files.Count} 张，去重后 {seen.Count} 张，重复 {dupCount} 张。" + Environment.NewLine + Environment.NewLine);
        foreach (var g in dupGroups.Take(10))
        {
            _out.AppendText("重复：" + Environment.NewLine);
            foreach (var p in g.Value.Take(4))
                _out.AppendText("  " + p.Replace(McRoot, ".minecraft") + Environment.NewLine);
            _out.AppendText(Environment.NewLine);
        }

        _status.Text = $"扫描完成：{files.Count} 张，重复 {dupCount} 张。";
    }

    private void Archive()
    {
        var files = Collect();
        if (files.Count == 0) { _status.Text = "没有可归档的截图。"; return; }

        var dest = @"I:\CCL\截图";
        try
        {
            Directory.CreateDirectory(dest);
            var seen = new HashSet<string>();
            var copied = 0;
            foreach (var f in files)
            {
                using (var sha = SHA256.Create())
                using (var fs = f.OpenRead())
                {
                    var h = Convert.ToHexString(sha.ComputeHash(fs));
                    if (!seen.Add(h)) continue;
                }

                var inst = f.Directory?.Parent?.Name ?? "未知实例";
                var target = Path.Combine(dest, inst + "_" + f.Name);
                if (!File.Exists(target))
                {
                    File.Copy(f.FullName, target);
                    copied++;
                }
            }

            _status.Text = $"归档完成：新增 {copied} 张 → {dest}";
            try { Process.Start(new ProcessStartInfo(dest) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex)
        {
            _status.Text = "归档失败：" + ex.Message;
        }
    }
}
