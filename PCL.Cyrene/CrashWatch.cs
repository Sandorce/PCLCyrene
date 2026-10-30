using System;
using System.IO;
using System.Linq;

namespace PCL.Cyrene;

/// <summary>
/// 崩溃看守：启动时看看有没有「新的」崩溃报告，有就问问要不要诊断。
/// </summary>
public static class CrashWatch
{
    private static string McRoot => Path.Combine(AppContext.BaseDirectory, ".minecraft");

    private static string StatePath =>
        Path.Combine(AppContext.BaseDirectory, "PCL", "last-crash.txt");

    public static void CheckAndShow()
    {
        try
        {
            var dir = Path.Combine(McRoot, "crash-reports");
            if (!Directory.Exists(dir)) return;

            var newest = Directory.GetFiles(dir, "*.txt")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null) return;

            var stamp = newest.LastWriteTimeUtc.Ticks.ToString();
            var last = "";
            try
            {
                if (File.Exists(StatePath)) last = File.ReadAllText(StatePath).Trim();
            }
            catch { }

            if (last == stamp) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                File.WriteAllText(StatePath, stamp);
            }
            catch { }

            if (DateTime.UtcNow - newest.LastWriteTimeUtc > TimeSpan.FromHours(24)) return;

            CyreneHost.ShowMessage?.Invoke(
                $"上次游戏崩了。\n\n报告：{newest.Name}\n\n要现在看看原因吗？",
                "崩溃诊断 · 昔涟",
                () => CyreneHost.ShowCrash?.Invoke());
        }
        catch
        {
            // 守护进程不许自己崩
        }
    }
}
