using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 整合包「直装化」工具：把 .mrpack 里需要联网下载的文件全部预先下好、塞进 overrides，
/// 再从 index 里移除这些条目，产出可以完全离线安装的包。
/// </summary>
public class WindowPackTool : Window
{
    private readonly TextBox _path = new();
    private readonly TextBlock _status = new();
    private readonly ProgressBar _bar = new();
    private readonly TextBox _log = new();
    private readonly Button _start = new();
    private readonly Button _browse = new();
    private bool _running;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PCL-PackTool/1.0");
        return c;
    }

    public WindowPackTool()
    {
        Title = "整合包直装工具 · 昔涟";
        Width = 640;
        Height = 540;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "把整合包变成「不用联网」的样子",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "选一个 .mrpack，工具会把里面所有需要下载的模组、资源包提前下好并校验，再重新打包。装的时候一个字节都不用联网。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 14),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        _path.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_path, 0);
        _browse.Content = "选择…";
        _browse.Height = 28;
        _browse.Click += (_, _) => Browse();
        Grid.SetColumn(_browse, 1);
        row.Children.Add(_path);
        row.Children.Add(_browse);
        panel.Children.Add(row);

        _start.Content = "开始转换";
        _start.Height = 34;
        _start.Width = 140;
        _start.Margin = new Thickness(0, 14, 0, 10);
        _start.HorizontalAlignment = HorizontalAlignment.Left;
        _start.Click += (_, _) => _ = StartAsync();
        panel.Children.Add(_start);

        _bar.Height = 8;
        _bar.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(_bar);

        _status.Text = "还没开始。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.Height = 200;
        _log.Margin = new Thickness(0, 10, 0, 0);
        _log.FontFamily = new FontFamily("Consolas");
        _log.FontSize = 11;
        _log.Background = new SolidColorBrush(Color.FromRgb(250, 240, 246));
        panel.Children.Add(_log);

        Content = panel;
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择整合包",
            Filter = "Modrinth 整合包 (*.mrpack)|*.mrpack|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true) _path.Text = dlg.FileName;
    }

    private void Log(string msg)
    {
        _log.AppendText(msg + Environment.NewLine);
        _log.ScrollToEnd();
    }

    private async Task StartAsync()
    {
        if (_running) return;
        var src = _path.Text.Trim();
        if (!File.Exists(src))
        {
            _status.Text = "先选一个存在的 .mrpack 文件。";
            return;
        }

        _running = true;
        _start.IsEnabled = false;
        _bar.Value = 0;
        _log.Clear();

        var work = Path.Combine(Path.GetTempPath(), "pcl-packtool-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(work);
            _status.Text = "正在解压……";
            Log("解压到 " + work);
            await Task.Run(() => ZipFile.ExtractToDirectory(src, work, true));

            var idxPath = Path.Combine(work, "modrinth.index.json");
            if (!File.Exists(idxPath))
            {
                _status.Text = "这个包里没有 modrinth.index.json，可能不是 Modrinth 格式。";
                return;
            }

            var overrides = Path.Combine(work, "client-overrides");
            Directory.CreateDirectory(overrides);

            var root = JsonNode.Parse(await File.ReadAllTextAsync(idxPath))!;
            var files = root["files"]!.AsArray();
            var total = files.Count;
            Log($"需要处理 {total} 个文件");

            var remove = new List<JsonNode?>();
            var okCount = 0;
            var fail = new List<string>();
            var done = 0;

            foreach (var node in files.ToList())
            {
                if (node is not JsonObject f)
                {
                    done++;
                    continue;
                }

                var rel = f["path"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar);
                var sha1 = f["hashes"]!["sha1"]!.GetValue<string>().ToLowerInvariant();
                var dest = Path.Combine(overrides, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                var got = File.Exists(dest) && Sha1(dest) == sha1;

                if (!got)
                {
                    var urls = new List<string>();
                    foreach (var u in f["downloads"]!.AsArray())
                    {
                        var raw = u!.GetValue<string>();
                        if (!urls.Contains(raw)) urls.Add(raw);
                        var m1 = raw.Replace("https://edge.forgecdn.net", "https://mediafilez.forgecdn.net");
                        if (m1 != raw && !urls.Contains(m1)) urls.Add(m1);
                        var m2 = raw.Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top")
                            .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top");
                        if (!urls.Contains(m2)) urls.Add(m2);
                    }

                    foreach (var url in urls)
                    {
                        try
                        {
                            var bytes = await Http.GetByteArrayAsync(url);
                            if (BytesSha1(bytes) == sha1)
                            {
                                await File.WriteAllBytesAsync(dest, bytes);
                                got = true;
                                break;
                            }
                        }
                        catch
                        {
                            // 换下一个源
                        }
                    }
                }

                if (got)
                {
                    okCount++;
                    remove.Add(node);
                }
                else
                {
                    fail.Add(rel);
                }

                done++;
                _bar.Value = done * 100.0 / Math.Max(1, total);
                _status.Text = $"处理中…… {done}/{total}　成功 {okCount}　失败 {fail.Count}";
                if (done % 10 == 0 || !got) Log($"[{done}/{total}] {(got ? "OK   " : "FAIL ")}{rel}");
            }

            foreach (var n in remove) files.Remove(n);
            await File.WriteAllTextAsync(idxPath, root.ToJsonString());

            if (fail.Count > 0)
            {
                Log($"留了 {fail.Count} 个没下下来的，安装时还会再试：");
                foreach (var x in fail.Take(20)) Log("  " + x);
            }

            _status.Text = "正在重新打包……";
            Log("重新打包中");

            var outPath = Path.Combine(
                Path.GetDirectoryName(src)!,
                Path.GetFileNameWithoutExtension(src) + "-直装.mrpack");
            if (File.Exists(outPath)) File.Delete(outPath);

            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(outPath, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(idxPath, "modrinth.index.json");
                foreach (var file in Directory.EnumerateFiles(overrides, "*", SearchOption.AllDirectories))
                {
                    var entryName = "client-overrides/" + Path.GetRelativePath(overrides, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
                }
            });

            _bar.Value = 100;
            _status.Text = "完成：" + outPath;
            Log("完成：" + outPath);
            Log("大小：" + new FileInfo(outPath).Length / 1024 / 1024 + " MB");
        }
        catch (Exception ex)
        {
            _status.Text = "出错了：" + ex.Message;
            Log("ERROR " + ex);
        }
        finally
        {
            _running = false;
            _start.IsEnabled = true;
            try { Directory.Delete(work, true); } catch { }
        }
    }

    private static string Sha1(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(s)).ToLowerInvariant();
    }

    private static string BytesSha1(byte[] data) =>
        Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
}
