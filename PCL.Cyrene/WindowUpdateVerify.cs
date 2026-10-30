using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL.Cyrene;

/// <summary>
/// 更新包校验：确认 zip 是完整的、里面有主程序，并给出 SHA256（方便对照）。
/// </summary>
public class WindowUpdateVerify : Window
{
    private readonly TextBox _path = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _out = new();

    public WindowUpdateVerify()
    {
        Title = "更新包校验 · 昔涟";
        Width = 680;
        Height = 460;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new LinearGradientBrush(Color.FromRgb(255, 250, 252), Color.FromRgb(253, 236, 244), 90);
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "这个包，能装吗？",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 70, 130))
        });

        panel.Children.Add(new TextBlock
        {
            Text = "先确认 zip 没截断、里面有 Cyrene Craft Launcher.exe，再给你它的 SHA256。坏包在安装前就会被拦下。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(120, 90, 110))
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        _path.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_path, 0);
        var browse = new Button { Content = "选择…", Height = 28 };
        browse.Click += (_, _) => Browse();
        Grid.SetColumn(browse, 1);
        row.Children.Add(_path);
        row.Children.Add(browse);
        panel.Children.Add(row);

        var check = new Button { Content = "校验", Height = 32, Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 8) };
        check.Click += (_, _) => Verify();
        panel.Children.Add(check);

        _status.Text = "选一个 .zip 更新包。";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(Color.FromRgb(110, 85, 105));
        panel.Children.Add(_status);

        _out.IsReadOnly = true;
        _out.AcceptsReturn = true;
        _out.TextWrapping = TextWrapping.Wrap;
        _out.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _out.Height = 200;
        _out.Margin = new Thickness(0, 10, 0, 0);
        _out.FontFamily = new FontFamily("Consolas");
        _out.FontSize = 11;
        _out.Padding = new Thickness(8);
        _out.Background = new SolidColorBrush(Color.FromRgb(252, 244, 249));
        _out.BorderThickness = new Thickness(1);
        _out.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 210, 225));
        panel.Children.Add(_out);

        Content = panel;
    }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择更新包",
            Filter = "更新包 (*.zip)|*.zip|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true) _path.Text = dlg.FileName;
    }

    private void Verify()
    {
        var file = _path.Text.Trim();
        if (!File.Exists(file)) { _status.Text = "文件不存在。"; return; }

        _out.Clear();
        try
        {
            var size = new FileInfo(file).Length;
            _out.AppendText($"文件：{Path.GetFileName(file)}" + Environment.NewLine);
            _out.AppendText($"大小：{size / 1024 / 1024} MB（{size} 字节）" + Environment.NewLine);

            if (size < 1024 * 1024)
            {
                _status.Text = "❌ 太小了，几乎肯定是坏的（小于 1 MB）。";
                return;
            }

            using var zip = ZipFile.OpenRead(file);
            var entries = zip.Entries.Count;
            var hasExe = zip.Entries.Any(e =>
                e.FullName.Equals("Cyrene Craft Launcher.exe", StringComparison.OrdinalIgnoreCase) ||
                e.FullName.EndsWith("/Cyrene Craft Launcher.exe", StringComparison.OrdinalIgnoreCase));

            _out.AppendText($"条目数：{entries}" + Environment.NewLine);
            _out.AppendText($"主程序：{(hasExe ? "有 ✅" : "没有 ❌")}" + Environment.NewLine);

            using var sha = SHA256.Create();
            using var fs = File.OpenRead(file);
            var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            _out.AppendText("SHA256：" + hash + Environment.NewLine);

            if (entries == 0 || !hasExe)
                _status.Text = "❌ 这个包不完整，别拿来更新。";
            else
                _status.Text = "✅ 结构完整。SHA256 记一下，可以和后缀对照。";
        }
        catch (Exception ex)
        {
            _status.Text = "❌ 打不开，包已损坏：" + ex.Message;
        }
    }
}
