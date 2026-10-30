using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 日志翻译：把 latest.log 里最常见的报错翻成人话。
/// </summary>
public class WindowLogDoctor : Window
{
    private readonly ComboBox _instances = new();
    private readonly TextBox _out = new();
    private readonly TextBlock _status = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowLogDoctor()
    {
        Title = "日志翻译 · 昔涟";
        Width = 760;
        Height = 620;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "日志里写了什么，人家念给你听",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "读最新的 latest.log，只挑出「会引发问题」的行，翻译成中文并给建议。",
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
        var run = new Button { Content = "翻译日志", Width = 100, Height = 28 };
        run.Click += (_, _) => Translate();
        top.Children.Add(run);
        panel.Children.Add(top);

        _out.IsReadOnly = true;
        _out.AcceptsReturn = true;
        _out.TextWrapping = TextWrapping.Wrap;
        _out.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _out.Height = 420;
        _out.FontSize = 12;
        _out.Padding = new Thickness(10);
        _out.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _out.BorderThickness = new Thickness(1);
        _out.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_out);

        _status.Text = "选一个实例，然后点「翻译日志」。";
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

    private static readonly (string Pattern, string Meaning, string Advice)[] Rules =
    [
        (@"UnsupportedClassVersionError|class file version", "Java 版本不对：字节码版本比当前 Java 高",
            "换对应版本的 Java（1.20.1 用 17，1.21 用 21），设置 → Java 里指定"),
        (@"OutOfMemoryError|Java heap space", "内存不够，Java 堆溢出了",
            "把内存调到 4096–8192 MB，别开太多后台程序"),
        (@"MixinApplyError|InvalidInjectionException|InvalidAccessorException|Critical injection failure",
            "Mixin 注入失败：某个模组想改的方法/字段不存在",
            "多半是模组版本和游戏版本不匹配。看日志里紧跟着的 mixin 名称，把那个模组换版本或删掉"),
        (@"NoSuchMethodError|NoClassDefFoundError", "找不到方法/类：依赖的库版本不对",
            "检查前置模组版本；同名不同版本的模组尤其常见（用「启动前自检」查）"),
        (@"Duplicate mods|duplicate mod", "同一个模组装了多个版本",
            "去 mods 删掉旧的那份"),
        (@"Pixel format not accelerated|GLFW error|OpenGL", "显卡/OpenGL 初始化失败",
            "更新显卡驱动；双显卡机器要让 Java 用独显"),
        (@"Failed to download|Connection refused|UnknownHostException|All connection attempts failed",
            "网络连不上，文件没下全",
            "换下载源到 BMCLAPI，或者用镜像；必要时重装该版本"),
        (@"java.io.IOException: Invalid paths argument", "库文件缺失（路径列表里没有真实文件）",
            "用「版本设置 → 补全文件」把缺的库补回来"),
        (@"zip END header not found|zip file is empty", "某个 jar 是坏的（半个文件/0 字节）",
            "找到那个 jar 重新下载；常见于下载中断"),
        (@"Failed to create window|Cannot create window", "窗口创建失败",
            "试试窗口化启动、更新显卡驱动，或换个分辨率"),
    ];

    private void Translate()
    {
        var inst = _instances.SelectedItem?.ToString();
        _out.Clear();
        if (string.IsNullOrEmpty(inst)) { _status.Text = "没有实例。"; return; }

        var dir = Path.Combine(McRoot, "versions", inst);
        var logPath = Path.Combine(dir, "logs", "latest.log");
        if (!File.Exists(logPath))
        {
            _status.Text = "这个实例还没有 latest.log（没启动过）。";
            return;
        }

        var text = ReadTail(logPath, 2_000_000);
        var sb = new StringBuilder();
        var hits = 0;

        foreach (var (pattern, meaning, advice) in Rules)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            hits++;
            var line = text.Substring(Math.Max(0, m.Index - 80), Math.Min(220, text.Length - Math.Max(0, m.Index - 80)))
                .Split('\n')[^1].Trim();

            sb.AppendLine($"● {meaning}");
            sb.AppendLine($"  原始：{line}");
            sb.AppendLine($"  建议：{advice}");
            sb.AppendLine();
        }

        if (hits == 0)
        {
            sb.AppendLine("没在日志里找到「明确的错误特征」。");
            sb.AppendLine();
            sb.AppendLine("这通常意味着：游戏其实没崩，或者问题出在启动器这一层（比如 Java、内存设置）。");
            sb.AppendLine("也可以去「崩溃诊断」看一眼 crash-reports。");
        }

        _out.Text = sb.ToString();
        _status.Text = hits == 0 ? "没发现明显错误。" : $"找到 {hits} 条可疑记录。";
    }

    private static string ReadTail(string path, int maxBytes)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var len = (int)Math.Min(fs.Length, maxBytes);
        fs.Seek(-len, SeekOrigin.End);
        var buf = new byte[len];
        _ = fs.Read(buf, 0, len);
        return Encoding.UTF8.GetString(buf);
    }
}
