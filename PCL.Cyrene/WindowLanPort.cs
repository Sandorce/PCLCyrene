using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 端口侦探：从游戏日志里把「对局域网开放」的端口找出来，省得回头翻聊天栏。
/// </summary>
public class WindowLanPort : Window
{
    private readonly TextBlock _result = new();
    private readonly TextBox _manual = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowLanPort()
    {
        Title = "端口侦探 · 昔涟";
        Width = 560;
        Height = 420;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "房间开在哪个端口？人家帮你找",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "游戏里「对局域网开放」之后，端口会写进 latest.log。点下面的按钮，人家去翻。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 14),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var find = new Button { Content = "帮我找端口", Height = 34, Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
        find.Click += (_, _) => Detect();
        panel.Children.Add(find);

        _result.Text = "还没找。";
        _result.TextWrapping = TextWrapping.Wrap;
        _result.FontSize = 15;
        _result.Margin = new Thickness(0, 16, 0, 8);
        _result.Foreground = new SolidColorBrush(Color.FromRgb(90, 60, 80));
        panel.Children.Add(_result);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
        var copy = new Button { Content = "复制端口", Width = 110, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
        copy.Click += (_, _) =>
        {
            var m = Regex.Match(_result.Text, @"\b(\d{2,5})\b");
            if (m.Success)
            {
                Clipboard.SetText(m.Groups[1].Value);
                _result.Text = "端口 " + m.Groups[1].Value + " 已复制。去樱花后台把它填成「本地端口」。";
            }
        };
        var open = new Button { Content = "打开樱花后台", Width = 130, Height = 30 };
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://www.natfrp.com/tunnel") { UseShellExecute = true }); }
            catch { }
        };
        row.Children.Add(copy);
        row.Children.Add(open);
        panel.Children.Add(row);

        panel.Children.Add(new TextBlock
        {
            Text = "也可以自己填（端口号）：",
            Margin = new Thickness(0, 8, 0, 4),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });
        _manual.Height = 28;
        panel.Children.Add(_manual);

        Content = panel;

        Loaded += (_, _) => Detect();
    }

    private void Detect()
    {
        try
        {
            var logs = Directory.Exists(Path.Combine(McRoot, "versions"))
                ? Directory.GetFiles(Path.Combine(McRoot, "versions"), "latest.log", SearchOption.AllDirectories)
                : [];
            var newest = logs.Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (newest is null)
            {
                _result.Text = "没找到游戏日志。先在游戏里开一次房间再试。";
                return;
            }

            var text = ReadTail(newest.FullName, 400_000);
            var matches = Regex.Matches(text, @"Local game hosted on port\s+(\d{2,5})");
            if (matches.Count == 0)
            {
                _result.Text = "日志里没有「对局域网开放」的记录。\n先在游戏里开房间，再点一次「帮我找端口」。";
                return;
            }

            var port = matches[^1].Groups[1].Value;
            _result.Text = $"找到了：端口 {port}\n（来自 {newest.Directory?.Parent?.Name}）";
            Clipboard.SetText(port);
        }
        catch (Exception ex)
        {
            _result.Text = "出错了：" + ex.Message;
        }
    }

    private static string ReadTail(string path, int maxBytes)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var len = (int)Math.Min(fs.Length, maxBytes);
        fs.Seek(-len, SeekOrigin.End);
        var buf = new byte[len];
        _ = fs.Read(buf, 0, len);
        return System.Text.Encoding.UTF8.GetString(buf);
    }
}
