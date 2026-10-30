using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 模组更新检查：算 jar 的 SHA1，问 Modrinth 有没有新版本。
/// </summary>
public class WindowModUpdate : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly TextBox _result = new();
    private readonly TextBlock _status = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowModUpdate()
    {
        Title = "模组更新检查 · 昔涟";
        Width = 680;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };

        var title = new TextBlock
        {
            Text = "你的模组是不是过期了？",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 8) };
        var scan = new Button { Content = "开始检查", Width = 130, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
        scan.Click += async (_, _) => await CheckAsync();
        var close = new Button { Content = "关闭", Width = 96, Height = 32 };
        close.Click += (_, _) => Close();
        row.Children.Add(scan);
        row.Children.Add(close);
        DockPanel.SetDock(row, Dock.Top);
        panel.Children.Add(row);

        _status.Text = "点「开始检查」：人家会算每个 jar 的哈希，去 Modrinth 查有没有新版。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 4, 0, 8);
        DockPanel.SetDock(_status, Dock.Top);
        panel.Children.Add(_status);

        _result.IsReadOnly = true;
        _result.AcceptsReturn = true;
        _result.TextWrapping = TextWrapping.Wrap;
        _result.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _result.FontSize = 12;
        _result.FontFamily = new FontFamily("Consolas");
        _result.Padding = new Thickness(8);
        _result.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _result.BorderThickness = new Thickness(1);
        _result.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_result);

        Content = panel;
    }

    private async Task CheckAsync()
    {
        _result.Clear();
        try
        {
            var modsDir = Path.Combine(McRoot, "mods");
            if (!Directory.Exists(modsDir))
            {
                _status.Text = "没有找到 .minecraft\\mods 文件夹。";
                return;
            }

            var jars = Directory.GetFiles(modsDir, "*.jar");
            if (jars.Length == 0)
            {
                _status.Text = "mods 里没有 jar。";
                return;
            }

            _status.Text = $"共 {jars.Length} 个模组，正在算哈希……";
            var byHash = new Dictionary<string, string>();
            foreach (var jar in jars)
            {
                try
                {
                    byHash[Sha1(jar)] = Path.GetFileName(jar);
                }
                catch { }
            }

            _status.Text = "正在问 Modrinth……";
            var body = JsonSerializer.Serialize(new
            {
                hashes = byHash.Keys.ToArray(),
                algorithm = "sha1"
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.modrinth.com/v2/version_files")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("User-Agent", "CCL-ModUpdate/1.0");

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _status.Text = "Modrinth 返回 " + (int)resp.StatusCode + "，稍后再试。";
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var found = new List<(string File, string ProjectId, string Current)>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var version = prop.Value;
                var projectId = version.GetProperty("project_id").GetString() ?? "";
                var current = version.GetProperty("version_number").GetString() ?? "";
                if (byHash.TryGetValue(prop.Name, out var file))
                    found.Add((file, projectId, current));
            }

            _result.AppendText($"能对上 Modrinth 的：{found.Count} / {jars.Length}\r\n\r\n");
            if (found.Count == 0)
            {
                _status.Text = "没有匹配到。可能这些 mod 不在 Modrinth 上（CurseForge 独家）。";
                return;
            }

            var outdated = 0;
            foreach (var (file, projectId, current) in found)
            {
                try
                {
                    var vreq = new HttpRequestMessage(HttpMethod.Get,
                        $"https://api.modrinth.com/v2/project/{projectId}/version");
                    vreq.Headers.TryAddWithoutValidation("User-Agent", "CCL-ModUpdate/1.0");
                    using var vresp = await Http.SendAsync(vreq);
                    if (!vresp.IsSuccessStatusCode) continue;

                    using var vdoc = JsonDocument.Parse(await vresp.Content.ReadAsStringAsync());
                    var latest = vdoc.RootElement.EnumerateArray()
                        .FirstOrDefault(v => v.GetProperty("version_type").GetString() == "release");
                    if (latest.ValueKind == JsonValueKind.Undefined) continue;

                    var latestNo = latest.GetProperty("version_number").GetString() ?? "";
                    if (latestNo == current) continue;

                    outdated++;
                    _result.AppendText($"{file}\r\n    当前 {current} → 最新 {latestNo}\r\n\r\n");
                }
                catch { }
            }

            _status.Text = outdated == 0
                ? $"检查完了：{found.Count} 个能对上，都已经是最新版。"
                : $"检查完了：{outdated} 个有新版本（下面是列表）。手动去 Modrinth 下新版替换即可。";
        }
        catch (Exception ex)
        {
            _status.Text = "出错了：" + ex.Message;
        }
    }

    private static string Sha1(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(s)).ToLowerInvariant();
    }
}
