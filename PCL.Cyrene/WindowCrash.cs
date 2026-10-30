using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 崩溃诊断：把 crash-report 翻成人话，告诉你大概是什么原因。
/// </summary>
public class WindowCrash : Window
{
    private readonly TextBox _text = new();

    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    public WindowCrash()
    {
        Title = "崩溃诊断 · 昔涟";
        Width = 660;
        Height = 580;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };

        var title = new TextBlock
        {
            Text = "这次为什么崩了？",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var again = new Button { Content = "重新分析", Width = 110, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
        again.Click += (_, _) => Analyze();
        var close = new Button { Content = "关闭", Width = 96, Height = 30 };
        close.Click += (_, _) => Close();
        buttons.Children.Add(again);
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        _text.IsReadOnly = true;
        _text.AcceptsReturn = true;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _text.FontSize = 13;
        _text.Padding = new Thickness(10);
        _text.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _text.BorderThickness = new Thickness(1);
        _text.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_text);

        Content = panel;
        Loaded += (_, _) => Analyze();
    }

    private void Analyze()
    {
        try
        {
            var dir = Path.Combine(McRoot, "crash-reports");
            string? newest = null;
            if (Directory.Exists(dir))
                newest = Directory.GetFiles(dir, "*.txt")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

            string? newestLog = null;
            var vdir = Path.Combine(McRoot, "versions");
            if (Directory.Exists(vdir))
                newestLog = Directory.GetFiles(vdir, "latest.log", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

            var source = newest ?? newestLog;
            if (source is null)
            {
                _text.Text = "没找到崩溃报告，也没找到游戏日志。\n\n说明崩溃可能发生在启动器这一层——看一下「设置 → 查看日志」里最新的那条。";
                return;
            }

            var content = ReadText(source);

            var sb = new StringBuilder();
            sb.AppendLine("看完了。人家说人话：");
            sb.AppendLine();

            var hints = 0;
            void Hint(string text)
            {
                sb.AppendLine("· " + text);
                hints++;
            }

            if (content.Contains("UnsupportedClassVersionError") ||
                content.Contains("class file version") ||
                content.Contains("Java 17") && content.Contains("not supported"))
                Hint("Java 版本不对。这个版本要 **Java 17**，你现在用的可能太新或太旧。\n  → 解决：装一个 Java 17，在「设置 → Java」里指过去。");

            if (Regex.IsMatch(content, @"OutOfMemoryError|Java heap space"))
                Hint("内存不够。\n  → 解决：设置里把内存调到 4096 MB 以上，别开太多后台程序。");

            if (content.Contains("java.lang.NoSuchMethodError") || content.Contains("NoClassDefFoundError"))
                Hint("模组版本不匹配（某个模组依赖的库版本不对）。\n  → 解决：检查最近新加的模组，或把它删掉试试。");

            if (content.Contains("Mixin apply failed") || content.Contains("mixin"))
                Hint("Mixin 冲突，通常是两个模组改了同一处代码。\n  → 解决：把最近加的模组一个个关掉，二分法找出来。");

            if (content.Contains("Pixel format not accelerated") || content.Contains("GLFW error") || content.Contains("OpenGL"))
                Hint("显卡驱动/OpenGL 的问题。\n  → 解决：更新显卡驱动；集成显卡记得给 Java 分配独立显卡。");

            if (content.Contains("Duplicate mods") || content.Contains("duplicate"))
                Hint("重复的模组（同一个模组放了两个版本）。\n  → 解决：去 mods 文件夹删掉旧的那个。");

            if (content.Contains("Failed to download") || content.Contains("Connection refused"))
                Hint("网络问题，补全没下完。\n  → 解决：换下载源后重新补全。");

            if (hints == 0)
                Hint("没有命中常见特征。把日志往下翻，找第一行以 \"Exception\" 或 \"Caused by\" 开头的，那通常是根因。");

            sb.AppendLine();
            sb.AppendLine("——");
            sb.AppendLine("分析依据：" + Path.GetFileName(source));
            _text.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            _text.Text = "分析失败：" + ex.Message;
        }
    }

    private static string ReadText(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var len = (int)Math.Min(fs.Length, 300_000);
            fs.Seek(-len, SeekOrigin.End);
            var buf = new byte[len];
            _ = fs.Read(buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }
        catch
        {
            return "";
        }
    }
}
