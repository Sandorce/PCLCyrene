using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCL.Cyrene;

/// <summary>
/// 樱花穿透：用 SakuraFrp 的 frpc 把本地端口映射到公网，只有开服的人需要跑。
/// </summary>
public class WindowFrp : Window
{
    private readonly TextBox _token = new();
    private readonly TextBox _tunnel = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _addrText = new();
    private readonly Button _start = new();
    private readonly TextBox _log = new();
    private readonly Button _stop = new();
    private Process _proc;
    private int _localPort;

    private static string ToolDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Frp");

    public WindowFrp()
    {
        Title = "樱花穿透 · 昔涟";
        Width = 480;
        Height = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "把房间开到公网",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "只有开服的人需要开这个。朋友拿到下面的地址，在游戏里「添加服务器」填进去就能进。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 16),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        panel.Children.Add(Label("访问密钥"));
        _token.Margin = new Thickness(0, 0, 0, 10);
        panel.Children.Add(_token);

        panel.Children.Add(Label("隧道 ID"));
        _tunnel.Margin = new Thickness(0, 0, 0, 16);
        panel.Children.Add(_tunnel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        _start.Content = "开启";
        _start.Width = 110;
        _start.Height = 34;
        _start.Margin = new Thickness(0, 0, 12, 0);
        _start.Click += (_, _) => StartTunnel();
        _stop.Content = "停止";
        _stop.Width = 110;
        _stop.Height = 34;
        _stop.IsEnabled = false;
        _stop.Click += (_, _) => StopTunnel();
        var help = new Button
        {
            Content = "联机帮助",
            Width = 110,
            Height = 34,
            Margin = new Thickness(12, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(170, 60, 120)),
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(170, 60, 120))
        };
        help.Click += (_, _) => new WindowHelp { Owner = this }.ShowDialog();
        buttons.Children.Add(_start);
        buttons.Children.Add(_stop);
        buttons.Children.Add(help);
        panel.Children.Add(buttons);

        _status.Text = "还没开启。";
        _status.Margin = new Thickness(0, 16, 0, 0);
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(90, 70, 85));
        panel.Children.Add(_status);

        _addrText.Text = "";
        _addrText.Margin = new Thickness(0, 10, 0, 0);
        _addrText.FontSize = 18;
        _addrText.FontWeight = FontWeights.SemiBold;
        _addrText.Foreground = new SolidColorBrush(Color.FromRgb(200, 60, 130));
        panel.Children.Add(_addrText);

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _log.Height = 110;
        _log.Margin = new Thickness(0, 12, 0, 0);
        _log.FontFamily = new FontFamily("Consolas");
        _log.FontSize = 11;
        _log.Background = new SolidColorBrush(Color.FromRgb(250, 240, 246));
        panel.Children.Add(_log);

        Content = panel;
        LoadSaved();
        Closing += (_, _) => StopTunnel();
    }

    private static string CredFile =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCL", "frp-credentials.txt");

    private void LoadSaved()
    {
        try
        {
            if (!File.Exists(CredFile)) return;
            var lines = File.ReadAllLines(CredFile);
            if (lines.Length > 0) _token.Text = lines[0].Trim();
            if (lines.Length > 1) _tunnel.Text = lines[1].Trim();
        }
        catch
        {
            // 读不到就空着
        }
    }

    private void SaveCred()
    {
        try
        {
            var dir = Path.GetDirectoryName(CredFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(CredFile, _token.Text.Trim() + Environment.NewLine + _tunnel.Text.Trim(),
                new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // 保存失败不影响使用
        }
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 0, 0, 6),
        Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105))
    };

    private void StartTunnel()
    {
        var exe = Path.Combine(ToolDir, "frpc.exe");
        if (!File.Exists(exe))
        {
            _status.Text = "找不到 frpc：" + exe;
            return;
        }

        var token = _token.Text.Trim();
        var tunnel = _tunnel.Text.Trim();
        if (token.Length == 0 || tunnel.Length == 0)
        {
            _status.Text = "访问密钥和隧道 ID 都要填。";
            return;
        }

        SaveCred();
        StopTunnel();
        try
        {
            foreach (var stale in Process.GetProcessesByName("frpc"))
            {
                try { stale.Kill(true); } catch { }
            }
        }
        catch
        {
            // 忽略
        }

        try
        {
            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"-f \"{token}:{tunnel}\"",
                    WorkingDirectory = ToolDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.GetEncoding(936),
                    StandardErrorEncoding = System.Text.Encoding.GetEncoding(936)
                }
            };
            _proc.OutputDataReceived += (_, e) => OnLine(e.Data);
            _proc.ErrorDataReceived += (_, e) => OnLine(e.Data);
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            _start.IsEnabled = false;
            _stop.IsEnabled = true;
            _status.Text = "正在连接樱花节点……";
        }
        catch (Exception ex)
        {
            _status.Text = "启动失败：" + ex.Message;
        }
    }

    private void OnLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Dispatcher.Invoke(() =>
        {
            try { _log.AppendText(line + Environment.NewLine); _log.ScrollToEnd(); } catch { }

            // 1) 优先取 >>host:port<< 这种被尖括号包起来的地址
            var m = Regex.Match(line, @">>\s*([^<>:\s]+):(\d{2,5})\s*<<");
            // 2) 退一步：任意域名 + 端口，但排除本机 / 内网
            if (!m.Success)
            {
                foreach (Match cand in Regex.Matches(line, @"([A-Za-z0-9][A-Za-z0-9\-\.]*\.[A-Za-z]{2,}):(\d{2,5})"))
                {
                    var host = cand.Groups[1].Value.ToLowerInvariant();
                    if (host.StartsWith("127.") || host == "localhost" ||
                        host.StartsWith("192.168.") || host.StartsWith("10.") ||
                        Regex.IsMatch(host, @"^172\.(1[6-9]|2\d|3[01])\."))
                        continue;
                    m = cand;
                    break;
                }
            }

            if (m.Success)
            {
                _addrText.Text = $"{m.Groups[1].Value}:{m.Groups[2].Value}";
                _status.Text = "已开启。把这个地址发给朋友。";
                try { Clipboard.SetText(_addrText.Text); } catch { }
                return;
            }

            var lp = Regex.Match(line, @"本地端口[^\d]*(\d{2,5})");
            if (lp.Success) _localPort = int.Parse(lp.Groups[1].Value);

            if (line.Contains("启动成功") || line.Contains("started successfully") || line.Contains("登录成功"))
            {
                _status.Text = "已连接樱花节点，正在读取公网地址……";
                CheckLocalPort();
            }
        });
    }

    /// <summary>
    /// 隧道连上了，但本地那个端口可能根本没人在听（游戏没开房间），要提醒使用者。
    /// </summary>
    private void CheckLocalPort()
    {
        if (_localPort <= 0) return;
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var task = client.ConnectAsync("127.0.0.1", _localPort);
            if (!task.Wait(800) || !client.Connected)
            {
                _status.Text = $"隧道已连上，但本地端口 {_localPort} 没有服务在听。先在游戏里「对局域网开放」，再回来点开启。";
            }
        }
        catch
        {
            _status.Text = $"隧道已连上，但本地端口 {_localPort} 连不上。先确认游戏已经开了房间。";
        }
    }

    private void StopTunnel()
    {
        try
        {
            if (_proc is { HasExited: false }) _proc.Kill(true);
        }
        catch
        {
            // 忽略
        }

        _proc = null;
        _start.IsEnabled = true;
        _stop.IsEnabled = false;
        _addrText.Text = "";
    }
}
