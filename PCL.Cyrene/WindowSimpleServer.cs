using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 简单开服：选一个服务端 jar，填内存和端口，点一下就跑起来。
/// </summary>
public class WindowSimpleServer : Window
{
    private readonly TextBox _dir = new();
    private readonly TextBox _jar = new();
    private readonly TextBox _mem = new() { Text = "4096" };
    private readonly TextBox _port = new() { Text = "25565" };
    private readonly CheckBox _online = new() { Content = "正版验证（online-mode）", IsChecked = false };
    private readonly TextBox _out = new();
    private readonly TextBlock _status = new();
    private readonly Button _start = new();
    private readonly Button _stop = new();
    private Process? _proc;
    private readonly ComboBox _ver = new();
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private static string JavaRoot => Path.Combine(AppContext.BaseDirectory, "PCL", "Java");

    public WindowSimpleServer()
    {
        Title = "简单开服 · 昔涟";
        Width = 760;
        Height = 640;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "把服务器开起来",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "选服务端 jar（forge / fabric / 原版都行），填内存和端口，点「开服」。会自动写 eula.txt。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        panel.Children.Add(Label("服务器目录"));
        var dirRow = new Grid();
        dirRow.ColumnDefinitions.Add(new ColumnDefinition());
        dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        _dir.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_dir, 0);
        var pickDir = new Button { Content = "选择…", Height = 28 };
        pickDir.Click += (_, _) => PickDir();
        Grid.SetColumn(pickDir, 1);
        dirRow.Children.Add(_dir);
        dirRow.Children.Add(pickDir);
        panel.Children.Add(dirRow);

        panel.Children.Add(Label("服务端 jar"));
        var jarRow = new Grid();
        jarRow.ColumnDefinitions.Add(new ColumnDefinition());
        jarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        _jar.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_jar, 0);
        var pickJar = new Button { Content = "选择…", Height = 28 };
        pickJar.Click += (_, _) => PickJar();
        Grid.SetColumn(pickJar, 1);
        jarRow.Children.Add(_jar);
        jarRow.Children.Add(pickJar);
        panel.Children.Add(jarRow);

        var dlRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        dlRow.Children.Add(new TextBlock { Text = "下载原版服务端：", VerticalAlignment = VerticalAlignment.Center });
        _ver.Width = 140;
        _ver.Height = 26;
        _ver.Margin = new Thickness(0, 0, 10, 0);
        dlRow.Children.Add(_ver);
        var dl = new Button { Content = "下载", Width = 90, Height = 28 };
        dl.Click += (_, _) => _ = DownloadServerAsync();
        dlRow.Children.Add(dl);
        panel.Children.Add(dlRow);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        row2.Children.Add(Label("内存 MB"));
        _mem.Width = 90;
        row2.Children.Add(_mem);
        row2.Children.Add(Label("  端口"));
        _port.Width = 90;
        row2.Children.Add(_port);
        _online.Margin = new Thickness(20, 0, 0, 0);
        _online.VerticalAlignment = VerticalAlignment.Center;
        row2.Children.Add(_online);
        panel.Children.Add(row2);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 10) };
        _start.Content = "开服";
        _start.Width = 120;
        _start.Height = 34;
        _start.Margin = new Thickness(0, 0, 10, 0);
        _start.Click += (_, _) => StartServer();
        _stop.Content = "停止";
        _stop.Width = 100;
        _stop.Height = 34;
        _stop.IsEnabled = false;
        _stop.Click += (_, _) => StopServer();
        actions.Children.Add(_start);
        actions.Children.Add(_stop);
        panel.Children.Add(actions);

        _status.Text = "还没开始。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _out.IsReadOnly = true;
        _out.AcceptsReturn = true;
        _out.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _out.FontFamily = new FontFamily("Consolas");
        _out.FontSize = 11;
        _out.Height = 240;
        _out.Margin = new Thickness(0, 8, 0, 0);
        _out.Padding = new Thickness(8);
        _out.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _out.BorderThickness = new Thickness(1);
        _out.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_out);

        Content = panel;
        Closing += (_, _) => StopServer();
        Loaded += (_, _) => _ = LoadVersionsAsync();
    }

    private static TextBlock Label(string s) => new()
    {
        Text = s,
        Margin = new Thickness(0, 8, 0, 4),
        Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
    };

    private void PickDir()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择服务器目录" };
        if (dlg.ShowDialog() == true) _dir.Text = dlg.FolderName;
    }

    private void PickJar()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择服务端 jar",
            Filter = "服务端 (*.jar)|*.jar|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _jar.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(_dir.Text))
                _dir.Text = Path.GetDirectoryName(dlg.FileName) ?? "";
        }
    }

    private static string FindJava()
    {
        try
        {
            if (Directory.Exists(JavaRoot))
            {
                var exe = Directory.GetFiles(JavaRoot, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exe is not null) return exe;
            }
        }
        catch { }

        return "java";
    }

    private void Log(string s)
    {
        _out.AppendText(s + Environment.NewLine);
        _out.ScrollToEnd();
    }

    private void StartServer()
    {
        if (_proc is not null && !_proc.HasExited) { _status.Text = "已经在跑了。"; return; }

        var dir = _dir.Text.Trim();
        var jar = _jar.Text.Trim();
        if (!Directory.Exists(dir)) { _status.Text = "先选服务器目录。"; return; }
        if (!File.Exists(jar)) { _status.Text = "先选服务端 jar。"; return; }

        try
        {
            // eula
            var eula = Path.Combine(dir, "eula.txt");
            File.WriteAllText(eula, "# 由 CCL 自动同意\n" + "eula=true\n");

            // server.properties：只改端口和正版验证，其余保留
            var props = Path.Combine(dir, "server.properties");
            var lines = File.Exists(props) ? File.ReadAllLines(props).ToList() : new List<string>();
            void Set(string key, string value)
            {
                var idx = lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) lines[idx] = key + "=" + value;
                else lines.Add(key + "=" + value);
            }

            Set("server-port", _port.Text.Trim());
            Set("online-mode", _online.IsChecked == true ? "true" : "false");
            File.WriteAllLines(props, lines);

            var memMb = int.TryParse(_mem.Text.Trim(), out var m) ? m : 4096;
            var java = FindJava();

            var psi = new ProcessStartInfo(java)
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add($"-Xmx{memMb}M");
            psi.ArgumentList.Add("-Xms" + Math.Max(1024, memMb / 2) + "M");
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(jar);
            psi.ArgumentList.Add("nogui");

            Log("$ " + java + $" -Xmx{memMb}M -jar {Path.GetFileName(jar)} nogui");
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.BeginInvoke(() => Log(e.Data)); };
            _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.BeginInvoke(() => Log(e.Data)); };
            _proc.Exited += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                _status.Text = "服务器已停止。";
                _start.IsEnabled = true;
                _stop.IsEnabled = false;
            });

            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            _start.IsEnabled = false;
            _stop.IsEnabled = true;
            _status.Text = $"已启动：端口 {_port.Text}，内存 {memMb} MB。";
        }
        catch (Exception ex)
        {
            _status.Text = "启动失败：" + ex.Message;
            Log("ERROR " + ex);
        }
    }

    private void StopServer()
    {
        try
        {
            if (_proc is not null && !_proc.HasExited)
            {
                _proc.Kill(true);
                _status.Text = "已停止。";
            }
        }
        catch { }

        _start.IsEnabled = true;
        _stop.IsEnabled = false;
    }

    // ---------------- 下载原版服务端 ----------------

    private async System.Threading.Tasks.Task LoadVersionsAsync()
    {
        try
        {
            var text = await Http.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            foreach (var v in doc.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (v.GetProperty("type").GetString() != "release") continue;
                _ver.Items.Add(v.GetProperty("id").GetString()!);
            }

            if (_ver.Items.Count > 0) _ver.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Log("版本列表加载失败：" + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task DownloadServerAsync()
    {
        var ver = _ver.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(ver)) { _status.Text = "先选一个版本。"; return; }

        try
        {
            _status.Text = $"正在查 {ver} 的服务端地址……";
            var manifest = await Http.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
            using var mdoc = System.Text.Json.JsonDocument.Parse(manifest);
            var url = "";
            foreach (var v in mdoc.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (v.GetProperty("id").GetString() != ver) continue;
                url = v.GetProperty("url").GetString()!;
                break;
            }

            if (string.IsNullOrEmpty(url)) { _status.Text = "找不到这个版本。"; return; }

            var vjson = await Http.GetStringAsync(url);
            using var vdoc = System.Text.Json.JsonDocument.Parse(vjson);
            if (!vdoc.RootElement.GetProperty("downloads").TryGetProperty("server", out var srv))
            {
                _status.Text = $"{ver} 没有官方服务端（太老的版本需要单独工具）。";
                return;
            }

            var dlUrl = srv.GetProperty("url").GetString()!;

            var dir = _dir.Text.Trim();
            if (!Directory.Exists(dir))
            {
                dir = Path.Combine(AppContext.BaseDirectory, "server-" + ver);
                Directory.CreateDirectory(dir);
                _dir.Text = dir;
            }

            var target = Path.Combine(dir, "server.jar");
            _status.Text = $"正在下载 {ver} 服务端……";
            var bytes = await Http.GetByteArrayAsync(dlUrl);
            await File.WriteAllBytesAsync(target, bytes);

            _jar.Text = target;
            Log($"已下载：{target}（{bytes.Length / 1024 / 1024} MB）");
            _status.Text = $"服务端已就位：{ver}。点「开服」就能跑。";
        }
        catch (Exception ex)
        {
            _status.Text = "下载失败：" + ex.Message;
        }
    }}
