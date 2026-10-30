using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 把已装的实例导出成 .mrpack，方便发给朋友。
/// 导出在后台线程跑，进度实时打到下面的日志区。
/// </summary>
public class WindowPackExport : Window
{
    private readonly TextBox _path = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _log = new();
    private readonly Button _export = new();
    private readonly Button _browse = new();
    private bool _running;

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowPackExport()
    {
        Title = "导出整合包 · 昔涟";
        Width = 660;
        Height = 560;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "把这个实例打包，发给朋友",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "选一个 versions 里的实例文件夹，人家把 mods、config、材质包一起打进 .mrpack。朋友直接导入就能玩。",
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

        _export.Content = "开始导出";
        _export.Height = 34;
        _export.Width = 140;
        _export.HorizontalAlignment = HorizontalAlignment.Left;
        _export.Margin = new Thickness(0, 14, 0, 10);
        _export.Click += (_, _) => _ = ExportAsync();
        panel.Children.Add(_export);

        _status.Text = "还没开始。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.Height = 240;
        _log.Margin = new Thickness(0, 10, 0, 0);
        _log.FontFamily = new FontFamily("Consolas");
        _log.FontSize = 11;
        _log.Background = new SolidColorBrush(Color.FromRgb(250, 240, 246));
        panel.Children.Add(_log);

        Content = panel;
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择实例文件夹",
            InitialDirectory = Path.Combine(McRoot, "versions")
        };
        if (dlg.ShowDialog() == true) _path.Text = dlg.FolderName;
    }

    private void Log(string line)
    {
        _log.AppendText(line + Environment.NewLine);
        _log.ScrollToEnd();
    }

    private async Task ExportAsync()
    {
        if (_running) return;
        var dir = _path.Text.Trim();
        if (!Directory.Exists(dir))
        {
            _status.Text = "先选一个存在的实例文件夹。";
            return;
        }

        _running = true;
        _export.IsEnabled = false;
        _browse.IsEnabled = false;
        _log.Clear();

        var progress = new Progress<string>(s =>
        {
            if (s.StartsWith("STATUS|", StringComparison.Ordinal))
                _status.Text = s[7..];
            else
                Log(s);
        });

        try
        {
            await Task.Run(() => DoExport(dir, progress));
        }
        catch (Exception ex)
        {
            _status.Text = "导出失败：" + ex.Message;
            Log("ERROR " + ex);
        }
        finally
        {
            _running = false;
            _export.IsEnabled = true;
            _browse.IsEnabled = true;
        }
    }

    private static void DoExport(string dir, IProgress<string> progress)
    {
        var name = Path.GetFileName(dir.TrimEnd('\\'));
        progress.Report($"开始处理：{name}");

        var (mc, loader, loaderVer) = DetectDependencies(dir, name, progress);
        var deps = new JsonObject { ["minecraft"] = mc };
        // 加载器版本必须是数字开头，否则安装器会 Convert.ToDouble 崩掉（"unknown" 就是这么炸的）
        if (!string.IsNullOrEmpty(loader) && Regex.IsMatch(loaderVer ?? "", @"^\d"))
        {
            deps[loader] = loaderVer;
        }
        else if (!string.IsNullOrEmpty(loader))
        {
            progress.Report($"加载器版本没读出来（{loaderVer}），这次只写原版；导入后请在「版本设置」里手动装一次 {loader}");
            loader = "";
            loaderVer = "";
        }
        progress.Report($"依赖：minecraft {mc}" + (string.IsNullOrEmpty(loader) ? "" : $"，{loader} {loaderVer}"));

        var index = new JsonObject
        {
            ["formatVersion"] = 1,
            ["game"] = "minecraft",
            ["versionId"] = "1.0.0",
            ["name"] = name,
            ["summary"] = "由 CCL 导出的实例",
            ["files"] = new JsonArray(),
            ["dependencies"] = deps
        };

        var parent = Path.GetDirectoryName(dir.TrimEnd('\\'))!;
        var outPath = Path.Combine(parent, name + "-导出.mrpack");
        if (File.Exists(outPath)) File.Delete(outPath);

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "logs", "crash-reports", "screenshots", "replay_recordings" };

        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(dir, f);
                var top = rel.Split(Path.DirectorySeparatorChar)[0];
                return !skip.Contains(top) && !rel.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var totalBytes = files.Sum(f => new FileInfo(f).Length);
        progress.Report($"共 {files.Count} 个文件，{totalBytes / 1024 / 1024} MB，开始打包……");
        progress.Report("STATUS|正在打包…… 0%");

        long doneBytes = 0;
        var doneFiles = 0;
        var lastPercent = -1;
        var lastLogPercent = -10;

        using (var zip = ZipFile.Open(outPath, ZipArchiveMode.Create))
        {
            var idx = zip.CreateEntry("modrinth.index.json");
            using (var w = new StreamWriter(idx.Open(), new UTF8Encoding(false)))
            {
                w.Write(index.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
            }

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                try
                {
                    zip.CreateEntryFromFile(file, "overrides/" + rel, CompressionLevel.Fastest);
                }
                catch
                {
                    // 单个文件锁住就跳过，不中断整包
                }

                doneFiles++;
                doneBytes += new FileInfo(file).Length;
                var percent = totalBytes == 0 ? 100 : (int)(doneBytes * 100 / totalBytes);

                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report($"STATUS|正在打包…… {percent}%（{doneBytes / 1024 / 1024} / {totalBytes / 1024 / 1024} MB，{doneFiles}/{files.Count} 个文件）");
                }

                if (percent / 10 > lastLogPercent / 10)
                {
                    lastLogPercent = percent;
                    progress.Report($"  [{percent,3}%] {rel}");
                }
            }
        }

        var mb = new FileInfo(outPath).Length / 1024 / 1024;
        progress.Report($"完成：{outPath}");
        progress.Report($"STATUS|导出完成：{name}-导出.mrpack（{mb} MB）");
    }

    /// <summary>
    /// 依赖检测：inheritsFrom → 版本 json 里的版本号字段 → 名字里的 1.x.x → 兜底 1.20.1
    /// </summary>
    private static (string Mc, string Loader, string LoaderVer) DetectDependencies(
        string dir, string name, IProgress<string> progress)
    {
        var mc = "";
        var loader = "";
        var loaderVer = "";

        var jsonPath = Path.Combine(dir, name + ".json");
        var idText = name;

        if (File.Exists(jsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: > 0 } idv)
                    idText = idv;

                if (root.TryGetProperty("inheritsFrom", out var inh) && inh.GetString() is { Length: > 0 } inherit)
                {
                    // inheritsFrom 通常形如 1.20.1-forge-47.4.20 或 1.20.1
                    var im = Regex.Match(inherit, @"^(?<mc>\d+\.\d+(?:\.\d+)?)(?:-(?<loader>[A-Za-z]+)-(?<ver>.+))?$");
                    if (im.Success)
                    {
                        mc = im.Groups["mc"].Value;
                        if (im.Groups["loader"].Success)
                        {
                            var lname = im.Groups["loader"].Value.ToLowerInvariant();
                            loader = lname switch
                            {
                                "forge" => "forge",
                                "neoforge" => "neoforge",
                                "fabric" => "fabric-loader",
                                "quilt" => "quilt-loader",
                                _ => ""
                            };
                            loaderVer = im.Groups["ver"].Value;
                        }
                    }
                    else
                    {
                        mc = inherit;
                    }
                }

                // 从 libraries 里认加载器：net.minecraftforge:forge:1.20.1-47.4.20 / fabric-loader 等
                if (root.TryGetProperty("libraries", out var libs) && libs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var lib in libs.EnumerateArray())
                    {
                        if (!lib.TryGetProperty("name", out var nameEl)) continue;
                        var nm = nameEl.GetString() ?? "";
                        var fm = Regex.Match(nm, @"^(?:net\.minecraftforge|net\.neoforged):forge:(\d+\.\d+(?:\.\d+)?)[-+]([\w.\-]+)$");
                        if (fm.Success)
                        {
                            mc = fm.Groups[1].Value;
                            loader = "forge";
                            loaderVer = fm.Groups[2].Value;
                            break;
                        }

                        var fm2 = Regex.Match(nm, @"^net\.neoforged:neoforge:([\w.\-]+)$");
                        if (fm2.Success)
                        {
                            loader = "neoforge";
                            loaderVer = fm2.Groups[1].Value;
                            break;
                        }

                        var ff = Regex.Match(nm, @"^net\.fabricmc:fabric-loader:([\w.\-]+)$");
                        if (ff.Success)
                        {
                            loader = "fabric-loader";
                            loaderVer = ff.Groups[1].Value;
                            break;
                        }

                        // 兜底：net.minecraftforge:fmlloader:1.20.1-47.4.16 这种「库名带版本」的写法
                        var fb = Regex.Match(nm, @"^net\.minecraftforge:[\w.\-]+:(\d+\.\d+(?:\.\d+)?)-([\w.\-]+)$");
                        if (fb.Success)
                        {
                            mc = fb.Groups[1].Value;
                            loader = "forge";
                            loaderVer = fb.Groups[2].Value;
                            break;
                        }

                        var fb2 = Regex.Match(nm, @"^net\.neoforged:[\w.\-]+:(\d+\.\d+(?:\.\d+)?)-([\w.\-]+)$");
                        if (fb2.Success)
                        {
                            mc = fb2.Groups[1].Value;
                            loader = "neoforge";
                            loaderVer = fb2.Groups[2].Value;
                            break;
                        }

                        var qq = Regex.Match(nm, @"^org\.quiltmc:quilt-loader:([\w.\-]+)$");
                        if (qq.Success)
                        {
                            loader = "quilt-loader";
                            loaderVer = qq.Groups[1].Value;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(mc))
                {
                    foreach (var key in new[] { "clientVersion", "minecraftVersion", "jar" })
                    {
                        if (root.TryGetProperty(key, out var k) && k.GetString() is { } v &&
                            Regex.IsMatch(v, @"^\d+\.\d+(\.\d+)?$"))
                        {
                            mc = v;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                progress.Report("读版本 json 失败：" + ex.Message);
            }
        }

        if (string.IsNullOrEmpty(mc))
        {
            var m = Regex.Match(idText + " " + name, @"(1\.\d{1,2}(?:\.\d{1,2})?)");
            if (m.Success)
            {
                mc = m.Groups[1].Value;
                progress.Report($"没找到 inheritsFrom，从名字里推断 MC 版本：{mc}（如果不对，导出后手动改一下 index）");
            }
        }

        // 兜底：绝不能拿实例名当版本号，否则安装器会去下载「乌托邦探险之旅3.5.1」的 json
        if (string.IsNullOrEmpty(mc) || !Regex.IsMatch(mc, @"^\d+\.\d+(\.\d+)?$"))
        {
            progress.Report($"没能确定原版版本，先按 1.20.1 写（导入若报「无对应的 json 下载地址」，就是这里不对）");
            mc = "1.20.1";
        }

        if (!string.IsNullOrEmpty(loader) && Regex.IsMatch(loaderVer ?? "", @"^\d"))
        {
            progress.Report($"从 inheritsFrom 读到了加载器：{loader} {loaderVer}");
            return (mc, loader, loaderVer);
        }

        var source = idText + " " + name;
        var forge = Regex.Match(source, @"forge[- ]?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
        var fabric = Regex.Match(source, @"fabric[- ]?loader[- ]?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
        var neoforge = Regex.Match(source, @"neoforge[- ]?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);

        if (forge.Success) { loader = "forge"; loaderVer = forge.Groups[1].Value; }
        else if (neoforge.Success) { loader = "neoforge"; loaderVer = neoforge.Groups[1].Value; }
        else if (fabric.Success) { loader = "fabric-loader"; loaderVer = fabric.Groups[1].Value; }
        else
        {
            // 再从 mods 里找线索：forge 的 mods.toml / fabric 的 fabric.mod.json
            var mods = Path.Combine(dir, "mods");
            if (Directory.Exists(mods))
            {
                if (Directory.GetFiles(mods, "*forge*", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    loader = "forge";
                    loaderVer = "";   // 只标记有 Forge，版本留空，由上层决定写不写
                }
            }
        }

        return (mc, loader, loaderVer);
    }
}
