using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 启动前自检：路径、Java、模组重名、备份情况。
/// 只报「真会让游戏起不来或丢东西」的项目。
/// </summary>
public class WindowPreflight : Window
{
    private readonly ComboBox _instances = new();
    private readonly TextBox _report = new();
    private readonly TextBlock _status = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");
    private static string JavaRoot => Path.Combine(AppContext.BaseDirectory, "PCL", "Java");

    public WindowPreflight()
    {
        Title = "启动前自检 · 昔涟";
        Width = 720;
        Height = 600;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "点启动之前，先看一眼",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "只查那些「真会让游戏起不来」或「会丢东西」的事：路径、Java、同名模组、存档备份。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(new TextBlock { Text = "实例：", VerticalAlignment = VerticalAlignment.Center });
        _instances.Width = 300;
        _instances.Height = 28;
        _instances.Margin = new Thickness(0, 0, 10, 0);
        top.Children.Add(_instances);
        var run = new Button { Content = "开始自检", Width = 100, Height = 28 };
        run.Click += (_, _) => Run();
        top.Children.Add(run);
        panel.Children.Add(top);

        _report.IsReadOnly = true;
        _report.AcceptsReturn = true;
        _report.TextWrapping = TextWrapping.Wrap;
        _report.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _report.Height = 380;
        _report.FontSize = 13;
        _report.Padding = new Thickness(10);
        _report.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _report.BorderThickness = new Thickness(1);
        _report.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_report);

        _status.Text = "选一个实例，然后点「开始自检」。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 10, 0, 0);
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

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

    private void Run()
    {
        var inst = _instances.SelectedItem?.ToString();
        _report.Clear();
        if (string.IsNullOrEmpty(inst)) { _status.Text = "没有实例。"; return; }

        var dir = Path.Combine(McRoot, "versions", inst);
        var lines = new List<string>();
        var problems = 0;

        void Ok(string s) => lines.Add("✅ " + s);
        void Warn(string s) { lines.Add("⚠️ " + s); problems++; }
        void Bad(string s) { lines.Add("❌ " + s); problems++; }

        // 1. 路径
        if (inst.Any(c => c > 127))
            Warn($"实例名含非 ASCII 字符（{inst}）。少数模组/Java 原生库在中文路径下会出问题，能改名更稳。");
        else
            Ok("路径是全 ASCII 字符。");

        // 2. Java
        var hasLocal = Directory.Exists(JavaRoot) &&
                       Directory.GetFiles(JavaRoot, "java.exe", SearchOption.AllDirectories).Length > 0;
        if (hasLocal) Ok("PCL\\Java 下有可用的 JDK。");
        else Warn("PCL\\Java 下没找到 JDK。如果游戏起不来，先去「Java 体检」下一个。");

        // 3. 模组同名 / 同 id 多版本
        var mods = Path.Combine(dir, "mods");
        if (Directory.Exists(mods))
        {
            var byId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var jar in Directory.GetFiles(mods, "*.jar"))
            {
                var id = ReadModId(jar);
                if (!byId.TryGetValue(id, out var list)) byId[id] = list = new List<string>();
                list.Add(Path.GetFileName(jar));
            }

            var dups = byId.Where(p => p.Value.Count > 1).ToList();
            if (dups.Count == 0) Ok($"{Directory.GetFiles(mods, "*.jar").Length} 个模组，没有同 id 多版本。");
            else
            {
                Warn($"发现 {dups.Count} 组「同一个模组装了多个版本」，这是最常见的崩溃原因：");
                foreach (var d in dups.Take(6))
                    lines.Add("    " + d.Key + " → " + string.Join(" / ", d.Value.Take(3)));
            }
        }

        // 4. 存档备份
        var saves = Path.Combine(dir, "saves");
        var backup = Path.Combine(dir, "saves-backup");
        var saveCount = Directory.Exists(saves) ? Directory.GetDirectories(saves).Length : 0;
        var backupCount = Directory.Exists(backup) ? Directory.GetDirectories(backup).Length : 0;
        if (saveCount == 0) Ok("没有存档，不用备份。");
        else if (backupCount == 0) Warn($"有 {saveCount} 个存档但从没备份过。建议先去「存档备份」点一下。");
        else Ok($"有 {saveCount} 个存档，已备份 {backupCount} 份。");

        // 5. 崩溃报告
        var crashes = Path.Combine(dir, "crash-reports");
        if (Directory.Exists(crashes))
        {
            var latest = Directory.GetFiles(crashes, "*.txt")
                .Select(p => new FileInfo(p)).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (latest is not null && (DateTime.Now - latest.LastWriteTime).TotalDays < 3)
                Warn($"最近三天崩过（{latest.Name}）。启动一次前，建议先看崩溃诊断。");
        }

        _report.Text = string.Join(Environment.NewLine, lines) +
                       Environment.NewLine + Environment.NewLine +
                       (problems == 0 ? "—— 没发现问题，放心启动。" : $"—— 有 {problems} 条需要注意（⚠️ 不会阻止启动）。");
        _status.Text = problems == 0 ? "一切正常。" : $"发现 {problems} 条，仅供参考。";
    }

    private static string ReadModId(string jar)
    {
        try
        {
            using var zip = ZipFile.OpenRead(jar);
            var toml = zip.GetEntry("META-INF/mods.toml") ?? zip.GetEntry("META-INF/neoforge.mods.toml");
            if (toml is not null)
            {
                using var r = new StreamReader(toml.Open());
                var m = Regex.Match(r.ReadToEnd(), @"modId\s*=\s*""([^""]+)""");
                if (m.Success) return m.Groups[1].Value.ToLowerInvariant();
            }

            var fabric = zip.GetEntry("fabric.mod.json");
            if (fabric is not null)
            {
                using var r = new StreamReader(fabric.Open());
                var m = Regex.Match(r.ReadToEnd(), @"""id""\s*:\s*""([^""]+)""");
                if (m.Success) return m.Groups[1].Value.ToLowerInvariant();
            }
        }
        catch { }

        return Path.GetFileNameWithoutExtension(jar).ToLowerInvariant();
    }
}
