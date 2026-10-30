using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 联机教程窗口：优先读 I:\CCL\联机教程.txt，读不到就用内置文本。
/// </summary>
public class WindowHelp : Window
{
    private const string BuiltIn = """
        ==== 联机教程（简版）====

        原理：开服的人跑一个「穿透」，把房间映射到公网；
              朋友什么都不用装，拿到一个地址就能进。

        一、主机先开房间
            游戏内：进入世界 → Esc → 「对局域网开放」→ 记住聊天栏里的端口号
            服务端：server.properties 里设 server-port=25565

        二、樱花后台建隧道
            https://www.natfrp.com → 隧道 → 创建隧道
            类型 TCP，本地端口 = 上一步的端口
            记下「隧道 ID」和「访问密钥」

        三、启动器点地球图标
            填密钥与隧道 ID → 开启 → 窗口里出现公网地址（自动复制）

        四、朋友加服务器
            多人游戏 → 添加服务器 → 填 IP:端口

        常见问题
            · 提示本地端口没有服务：游戏房间没开，或端口填错
            · 无效会话：局域网世界默认正版验证，改用服务端并设 online-mode=false
            · 地址是 127.0.0.1：升级到 1.0.4 以上
            · 日志乱码：升级到 1.0.5 以上

        完整版见：I:\CCL\联机教程.txt
        """;

    public WindowHelp()
    {
        Title = "联机教程 · 昔涟";
        Width = 660;
        Height = 600;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 246, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };

        var title = new TextBlock
        {
            Text = "怎么把这个房间开到公网",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var close = new Button
        {
            Content = "知道了",
            Width = 96,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);

        var box = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(252, 244, 249)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225)),
            Text = LoadText()
        };
        panel.Children.Add(box);

        Content = panel;
    }

    private static string LoadText()
    {
        foreach (var path in new[]
                 {
                     @"I:\CCL\联机教程.txt",
                     Path.Combine(AppContext.BaseDirectory, "联机教程.txt")
                 })
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            catch
            {
                // 读不到就试下一个
            }
        }

        return BuiltIn;
    }
}
