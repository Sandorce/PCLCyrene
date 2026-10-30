using System.Collections.Concurrent;

namespace PCL.Network;

/// <summary>
///     按主机记录下载源的实测吞吐，用于在多个下载源之间按速度排序。
///     目的：国内直连官方 CDN 时经常是"慢而不断"，旧逻辑只有抛异常才会换源，
///     结果整批文件都被同一个慢源拖住。
/// </summary>
internal static class DownloadSourceSpeed
{
    /// <summary>未知源的初始分数，单位 MB/s。故意取中间值，让已知快的源优先、已知慢的源靠后。</summary>
    private const double UnknownScore = 1d;

    private const double MinScore = 0.05d;

    /// <summary>指数平滑系数，越小越迟钝，避免单次抖动就改顺序。</summary>
    private const double Smoothing = 0.4d;

    private static readonly ConcurrentDictionary<string, double> Scores =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按历史吞吐从快到慢排序；分数相同的源保持原有顺序（LINQ 排序是稳定的）。</summary>
    public static List<string> Order(List<string> urls)
    {
        if (urls.Count < 2)
            return urls;

        var ordered = urls.OrderByDescending(GetScore).ToList();
        if (!ordered.SequenceEqual(urls))
            ModBase.Log("[Download] 下载源按实测速度排序：" + string.Join(" > ",
                    ordered.Select(url => GetHost(url) + "(" + GetScore(url).ToString("F2") + " MB/s)")),
                ModBase.LogLevel.Debug);
        return ordered;
    }

    public static void RecordSuccess(string url, long bytes, TimeSpan elapsed)
    {
        if (bytes <= 0 || elapsed <= TimeSpan.Zero)
            return;

        var mbps = bytes / 1048576d / elapsed.TotalSeconds;
        if (mbps <= 0d)
            return;

        var host = GetHost(url);
        Scores.AddOrUpdate(host, mbps, (_, old) => old * (1d - Smoothing) + mbps * Smoothing);
    }

    public static void RecordFailure(string url)
    {
        Scores.AddOrUpdate(GetHost(url), MinScore, (_, old) => Math.Max(MinScore, old * 0.5d));
    }

    public static double GetScore(string url)
    {
        return Scores.TryGetValue(GetHost(url), out var score) ? score : UnknownScore;
    }

    private static string GetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
    }
}
