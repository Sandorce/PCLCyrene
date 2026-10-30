using System;

namespace PCL.Cyrene;

/// <summary>
/// 定制模块与启动器主程序之间的唯一通道。
/// 模块本身不认识 PCL 的窗口，只往这里丢请求，由主程序接住。
/// </summary>
public static class CyreneHost
{
    /// <summary>弹一个提示框：(正文, 标题, 确认后动作)。主程序启动时注入。</summary>
    public static Action<string, string, Action?>? ShowMessage { get; set; }

    /// <summary>弹一条右上角小提示：(正文)。</summary>
    public static Action<string>? ShowToast { get; set; }

    /// <summary>安装一个 .mrpack：(文件路径)。</summary>
    public static Action<string>? InstallModpack { get; set; }

    /// <summary>打开崩溃诊断窗口。</summary>
    public static Action? ShowCrash { get; set; }
}
