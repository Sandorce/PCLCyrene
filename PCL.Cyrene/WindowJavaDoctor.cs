using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// Java 体检：看看这个实例要哪个 Java、机器上有哪些、缺了就直接下。
/// </summary>
public class WindowJavaDoctor : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly TextBox _log = new();
    private readonly TextBlock _status = new();
    private readonly Button _download = new() { Content = "下载 Java 17", Width = 140, Height = 34, IsEnabled = false };
    private readonly Button _openFolder = new() { Content = "打开 Java 目录", Width = 140, Height = 34, Margin = new Thickness(10, 0, 0, 0) };

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");
    private static string JavaRoot => Path.Combine(AppContext.BaseDirectory, "PCL", "Java");

    private int _need = 17;

    public WindowJavaDoctor()
    {
        Title = "Java 体检 · 昔涟";
        Width = 660;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };

        var title = new TextBlock
        {
            Text = "这个包该用哪个 Java？",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        _download.Click += async (_, _) => await DownloadAsync();
        _openFolder.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(JavaRoot);
                Process.Start(new ProcessStartInfo(JavaRoot) { UseShellExecute = true });
            }
            catch { }
        };
        var again = new Button { Content = "重新检测", Width = 110, Height = 34, Margin = new Thickness(0, 0, 10, 0) };
        again.Click += (_, _) => Scan();
        row.Children.Add(again);
        row.Children.Add(_download);
        row.Children.Add(_openFolder);
        DockPanel.SetDock(row, Dock.Top);
        panel.Children.Add(row);

        _status.Text = "检测中……";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 12, 0, 8);
        DockPanel.SetDock(_status, Dock.Top);
        panel.Children.Add(_status);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.FontSize = 12;
        _log.FontFamily = new FontFamily("Consolas");
        _log.Padding = new Thickness(8);
        _log.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _log.BorderThickness = new Thickness(1);
        _log.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_log);

        Content = panel;
        Loaded += (_, _) => Scan();
    }

    private void Scan()
    {
        _log.Clear();
        try
        {
            var versions = Directory.Exists(Path.Combine(McRoot, "versions"))
                ? Directory.GetDirectories(Path.Combine(McRoot, "versions"))
                : [];

            var needList = new List<string>();
            foreach (var dir in versions)
            {
                var json = Path.Combine(dir, Path.GetFileName(dir) + ".json");
                if (!File.Exists(json)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(json));
                    if (doc.RootElement.TryGetProperty("javaVersion", out var jv) &&
                        jv.TryGetProperty("majorVersion", out var major))
                    {
                        var n = major.GetInt32();
                        needList.Add($"{Path.GetFileName(dir)} → Java {n}");
                        if (_need == 17) _need = n;
                    }
                }
                catch { }
            }

            _log.AppendText("== 实例需要的 Java ==\r\n");
            if (needList.Count == 0) _log.AppendText("（版本 json 里没写，默认按 Java 17 处理）\r\n");
            foreach (var s in needList) _log.AppendText("  " + s + "\r\n");

            var found = FindJavas();
            _log.AppendText("\r\n== 机器上找到的 Java ==\r\n");
            if (found.Count == 0) _log.AppendText("  一个都没找到\r\n");
            foreach (var (path, ver) in found)
                _log.AppendText($"  Java {ver}  {path}\r\n");

            var hasNeed = found.Any(x => x.Version == _need);
            if (hasNeed)
            {
                _status.Text = $"有 Java {_need}，可以直接用。";
                _download.IsEnabled = false;
            }
            else
            {
                _status.Text = $"没有 Java {_need}。点「下载 Java {_need}」让人家下一个放这儿，再去「设置 → Java」里选它。";
                _download.Content = $"下载 Java {_need}";
                _download.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _status.Text = "检测失败：" + ex.Message;
        }
    }

    private static List<(string Path, int Version)> FindJavas()
    {
        var result = new List<(string, int)>();
        var roots = new List<string>
        {
            JavaRoot,
            @"C:\Program Files\Java",
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\BellSoft",
            @"C:\Program Files\Zulu"
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> exes;
            try { exes = Directory.EnumerateFiles(root, "java.exe", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var exe in exes.Take(40))
            {
                if (!exe.Replace('/', '\\').EndsWith(@"\bin\java.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var ver = ProbeVersion(exe);
                if (ver > 0) result.Add((exe, ver));
            }
        }

        return result
            .GroupBy(x => x.Item2)
            .Select(g => g.First())
            .OrderBy(x => x.Item2)
            .ToList();
    }

    private static int ProbeVersion(string javaExe)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(javaExe, "-version")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return 0;
            var text = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            var m = System.Text.RegularExpressions.Regex.Match(text, @"version ""(\d+)(\.(\d+))?");
            if (!m.Success) return 0;
            var major = int.Parse(m.Groups[1].Value);
            if (major == 1 && m.Groups[3].Success) major = int.Parse(m.Groups[3].Value);
            return major;
        }
        catch
        {
            return 0;
        }
    }

    private async Task DownloadAsync()
    {
        _download.IsEnabled = false;
        try
        {
            Directory.CreateDirectory(JavaRoot);
            var zip = Path.Combine(JavaRoot, $"temurin-{_need}.zip");
            var url = $"https://api.adoptium.net/v3/binary/latest/{_need}/ga/windows/x64/jdk/hotspot/normal/eclipse";

            _status.Text = "正在下载……这个有 180 MB 左右，慢的话喝口水。";
            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? 0;
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(zip);
                var buf = new byte[262144];
                long got = 0;
                int read;
                while ((read = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read));
                    got += read;
                    if (total > 0)
                        _status.Text = $"正在下载…… {got * 100 / total}%（{got / 1024 / 1024} / {total / 1024 / 1024} MB）";
                }
            }

            _status.Text = "下载完了，正在解压……";
            var extractDir = Path.Combine(JavaRoot, $"jdk-{_need}");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(zip, JavaRoot);
            var inner = Directory.GetDirectories(JavaRoot, "jdk-*").OrderByDescending(Directory.GetLastWriteTime).FirstOrDefault();
            if (inner is not null && inner != extractDir) Directory.Move(inner, extractDir);
            File.Delete(zip);

            Scan();
            _status.Text = $"装好了：{Path.Combine(extractDir, "bin", "java.exe")}\r\n去「设置 → Java」点刷新，把它选上。";
        }
        catch (Exception ex)
        {
            _status.Text = "下载失败：" + ex.Message;
            _download.IsEnabled = true;
        }
    }
}
