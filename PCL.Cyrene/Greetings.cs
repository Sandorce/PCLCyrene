using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;


namespace PCL.Cyrene;

/// <summary>
/// 陪伴向的小东西：分时问候、每日一句、游戏时长提醒。
/// </summary>
public static class Greetings
{
    private static readonly string[] Quotes =
    [
        "慢一点也没关系，反正路还长。",
        "今天也要好好吃饭呀。",
        "累了就把游戏关掉，趴一会儿。",
        "你不需要每次都做到最好。",
        "有人在你身后，不用回头看也知道。",
        "把窗户开一条缝吧，今天的风很好。",
        "偶尔什么都不做，也是一种正经事。",
        "你写下的东西，时间会替你记着。",
        "怕的时候，就先深呼吸三次。",
        "这世界大得很，别把自己困在小事里。",
        "你不欠任何人一个解释。",
        "今天的月亮，和昨天不太一样。",
        "想哭就哭，人家不笑你。",
        "如果你在等一个理由，就当是「因为想」。",
        "别把「忍一下」当成习惯。",
        "偶尔对自己好一点，不丢人。"
    ];

    private static int _playMinutes;
    private static int _lastWarnedHour;
    private static DispatcherTimer? _timer;

    /// <summary>启动时调用：一句分时问候 + 今日的一句。</summary>
    public static void OnStartup()
    {
        var hour = DateTime.Now.Hour;
        var greeting = TimeGreeting(hour);
        var quote = Quotes[DateTime.Now.DayOfYear % Quotes.Length];

        CyreneHost.ShowToast?.Invoke($"{greeting}\n{quote}");
        StartPlayWatch();
    }

    private static string TimeGreeting(int hour) => hour switch
    {
        0 or 1 => "这么晚了……别熬太久，好吗。",
        2 or 3 or 4 => "天快亮了，你还没睡呀。",
        5 or 6 => "早上好，天刚亮呢。",
        7 or 8 or 9 => "早上好呀，今天也要慢一点。",
        10 or 11 => "上午好，记得喝口水。",
        12 or 13 => "中午了，吃过了吗？",
        14 or 15 or 16 => "下午好，犯困的话可以眯一会儿。",
        17 or 18 => "傍晚了呢，天要黑了。",
        19 or 20 or 21 => "晚上好，今天辛苦啦。",
        _ => "夜深了，玩一会儿就去睡吧。"
    };

    private static void StartPlayWatch()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private static void Tick()
    {
        try
        {
            var running = Process.GetProcesses()
                .Any(p =>
                {
                    try
                    {
                        return p.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase) ||
                               p.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (!running)
            {
                _playMinutes = 0;
                _lastWarnedHour = 0;
                return;
            }

            _playMinutes++;
            var hours = _playMinutes / 60;
            if (hours >= 2 && hours > _lastWarnedHour && hours % 2 == 0)
            {
                _lastWarnedHour = hours;
                CyreneHost.ShowToast?.Invoke($"已经玩了 {hours} 小时啦。\n站起来走两步，喝口水再继续。");
            }
        }
        catch
        {
            // 静默
        }
    }
}
