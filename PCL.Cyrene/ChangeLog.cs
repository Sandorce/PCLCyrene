using System;
using System.IO;
using System.Windows;

namespace PCL.Cyrene;

/// <summary>
/// 版本更新公告：启动器升级后，第一次打开时在主界面弹一次「更新内容」。
/// 每次发版请在这里追加一条，并把 CurrentVersion 一并改掉。
/// </summary>
public static class ChangeLog
{
    /// <summary>当前版本号（每次发版改这里）</summary>
    public const string CurrentVersion = "1.14.0";

    private static readonly (string Version, string Notes)[] Entries =
    [
        ("1.14.0", "· 更新页新增「检查更新」：一键打开官网下载更新包（地址可用 PCL\\site-url.txt 配置）"),
        ("1.13.0", "· 个性化新增「主题色」：粉色 / 蓝色一键切换（默认蓝，符合上游许可）"),
        ("1.12.0", "· 合规化发布版：名称改为「PCL 昔涟版」，主色相改回蓝色，关于页补上游署名"),
        ("1.11.0", "· 正式版：合入 beta.1–beta.11 全部改动（存档备份 / 启动前自检 / 日志翻译 / 更新包校验 / 截图归档 / 简单开服 / 品牌清理 / 新图标）"),
        ("1.11.0-beta.11", "· 移除「整合包市场」（数据源与 mcmod 不一致，用处不大）"),
        ("1.11.0-beta.10", "· 新增「整合包市场」：从 Modrinth 搜整合包，选中即下载并交给启动器安装"),
        ("1.11.0-beta.9", "· 简单开服新增「下载原版服务端」：列官方版本，直接下 server.jar 并填好路径"),
        ("1.11.0-beta.8", "· 标题栏新增「服务器」图标（在穿透左边），点一下就能开服"),
        ("1.11.0-beta.7", "· 新增「简单开服」：选服务端 jar → 自动写 eula / server.properties → 后台运行并显示控制台输出"),
        ("1.11.0-beta.6", "· 换掉最后一张 PCL 图标：icon.ico 改用昔涟头像（16–256 多尺寸）"),
        ("1.11.0-beta.5", "· 清理界面里残留的 PCL CE / Plain Craft Launcher 字样，统一为 CCL / Cyrene Craft Launcher"),
        ("1.11.0-beta.4", "· 新增「更新包校验」：查完整性 + 主程序 + SHA256\n· 新增「截图归档」：跨实例汇总、按哈希去重、归档到 I:\\CCL\\截图"),
        ("1.11.0-beta.3", "· 新增「日志翻译」：把 latest.log 里的常见报错翻成中文并给建议（10 类特征）"),
        ("1.11.0-beta.2", "· 新增「启动前自检」：路径 / Java / 同 id 多版本模组 / 存档备份 / 近期崩溃，五项一次查完"),
        ("1.11.0-beta.1", "· 新增「存档备份」：一键备份 saves 到 saves-backup\\时间戳，只留最近 5 份，可回滚"),
        ("1.10.0", "· 移除「补全模组」功能（判定太宽，误报多，不如直接看崩溃日志）"),
        ("1.9.4", "· 补全模组：jarjar 嵌套库算「已提供」（不再误报）；fabric-api 等加载器级依赖排除"),
        ("1.9.3", "· 修联网补全：模组 id 先用 Modrinth 搜索解析成真实 slug，`fabric`/`quilt` 等加载器标识不再当模组查"),
        ("1.9.2", "· 补全模组新增 CurseForge 下载源：Modrinth 找不到时自动改问 cfwidget（无需 API Key）"),
        ("1.9.1", "· 补全模组支持联网：本地找不到的，按 modId 去 Modrinth 拉对应 MC/加载器版本"),
        ("1.9.0", "· 新增「补全模组」：读每个 jar 的依赖声明，找出缺失的前置，从 mods-disabled 与资源库里一键找回"),
        ("1.8.2", "· 导出加载器识别再补一层兜底：net.minecraftforge:fmlloader:1.20.1-47.x 这种「库名带版本」也能认出来"),
        ("1.8.1", "· 修复导出整合包读不到加载器：改为从 libraries 里识别 forge / neoforge / fabric / quilt 版本"),
        ("1.8.0", "· 正式版：把 beta.7–beta.15 的全部成果合入\n· PCL.Cyrene 独立模块（15 个文件）\n· 资源库 / 崩溃看守 / 更新历史 / 同一个出处\n· 下载提速：单文件 24 连接、全局 192、并发 160、小文件阈值 256KB"),
        ("1.7.0-beta.15", "· 下载再提速：小文件阈值 1MB→256KB、分块 2MB→1MB、单文件连接上限 16→24、全局 128→192、并发文件 96→160\n· 慢块判定放宽到 1/12 且观察 20 秒，收尾阶段不再主动减少连接"),
        ("1.7.0-beta.14", "· 修掉「更新内容」弹窗点确定不关（传了 button1Action 的副作用）"),
        ("1.7.0-beta.13", "· 修掉更新页多出来的空白「更新历史」卡（beta.12 的排版事故）"),
        ("1.7.0-beta.12", "· 资源库顺带列出 I:\\CCL 根目录的 .mrpack（本机整合包）\n· 更新页新增「同一个出处」说明：安装包与更新包内容一致"),
        ("1.7.0-beta.11", "· P2 收尾：资源库迁入 PCL.Cyrene（整合包安装经 CyreneHost.InstallModpack 转交），定制代码全部归位"),
        ("1.7.0-beta.10", "· 结构：樱花穿透 / 直装工具 / 导出 / 模组检查 / 教程 / 历史 六个窗口迁入 PCL.Cyrene，定制代码基本归位"),
        ("1.7.0-beta.9", "· 结构：端口侦探 / 崩溃诊断 / Java 体检 三个窗口迁入 PCL.Cyrene"),
        ("1.7.0-beta.8", "· 结构：ChangeLog / Greetings / CrashWatch 搬进 PCL.Cyrene，主程序通过 CyreneHost 注入界面能力"),
        ("1.7.0-beta.7", "· 结构：新增独立模块 PCL.Cyrene（定制功能以后住这里，升级上游时不必逐行对齐）\n· 本次只是立壳，行为与 beta.6 一致"),
        ("1.7.0-beta.6", "· 崩溃看守：启动时发现 24 小时内的新崩溃报告，会主动问你要不要看原因\n· 同一个报告只问一次，不会天天烦你"),
        ("1.7.0-beta.5", "· 标题栏两个图标换成更明确的样式（直装=包裹+，穿透=网络）并放大\n· 工具窗口背景加一层淡粉渐变"),
        ("1.7.0-beta.4", "· 界面圆角统一放大：卡片 5→10、按钮 3→8、勾选框 3→5、对话框 7→10\n· 侧边栏高亮块 3→6，整体更圆润"),
        ("1.7.0-beta.3", "· 小工具那排按钮改成自动换行，资源库不会再被挤出卡片外"),
        ("1.7.0-beta.2", "· 新增「资源库」（设置 → 杂项 → 小工具）：扫 I:\\CCL\\资源库，光影/资源包一键复制到实例，mrpack 一键安装"),
        ("1.7.0-beta.1", "· 测试版：为「内置资源库」预留结构与入口\n· 其余功能与 1.6.4 一致"),
        ("1.6.4", "· 修复导出包写进 forge unknown 导致 FormatException\n· 加载器版本读不出来时只写原版，并提示导入后手动装"),
        ("1.6.3", "· 修复导出整合包把实例名当版本号（导致「无对应的 json 下载地址」）\n· 改为从 inheritsFrom 解析原版版本与加载器"),
        ("1.6.2", "· 导出整合包重做：进度实时显示、导出不再卡住界面、依赖检测更准\n· 构建脚本统一：安装包与更新包出处相同，内容永远一致"),
        ("1.6.0", "· 新增「导出整合包」：把实例连 mods 一起打成 .mrpack，直接发给朋友\n· 新增「模组更新检查」：按哈希问 Modrinth，列出所有过期模组"),
        ("1.5.0", "· 分时问候：早上 / 中午 / 傍晚 / 深夜，说的话不一样\n· 每日一句：每天换一句，按日期轮转\n· 游戏时长提醒：连玩两小时起，每两小时提醒一次起来走走"),
        ("1.4.2", "· 设置 → 个性化新增「启动时播放问候语音」开关"),
        ("1.4.1", "· 新增「Java 体检」：按实例判断该用哪个 Java，缺了直接下载安装到 PCL的Java目录"),
        ("1.4.0", "· 新增「端口侦探」：从游戏日志里读出局域网端口，自动复制\n· 新增「崩溃诊断」：把 crash-report 翻成人话，告诉你大概哪里出问题"),
        ("1.3.2", "· 樱花穿透窗口新增「联机帮助」按钮，一键打开联机教程"),
        ("1.3.1", "· 修复更新历史卡片文字与标题重叠"),
        ("1.3.0", "· 更新页新增「查看历史更新」：一个窗口翻看全部版本的改动"),
        ("1.2.2", "· 下载源改按实测速度自动排序：慢源不再拖住整批文件\n· 单个源失败会被降权，下次优先试更快的那个"),
        ("1.2.1", "· 修复标题栏版本徽章不跟随版本（赋值早于 InitializeComponent）\n· 更新页隐藏残留的「查看更新日志」按钮"),
        ("1.2.0", "· 并行度再提：小文件阈值 4MB→1MB、分块 4MB→2MB、并发文件 63→96"),
        ("1.1.9", "· 稳定下载：分块更细（8→4 MB）、判定更宽容（1/4→1/6）、重试 3 次、缓冲 256KB"),
        ("1.1.8", "· 下载提速：单文件并发上限 8 → 16，全局连接 64 → 128，缓冲区 64KB → 256KB"),
        ("1.1.7", "· 标题栏「CE」徽章改为版本号，并自动跟随版本"),
        ("1.1.6", "· 语言页去掉「让 PCL CE 走向国际化」横幅与两个外链按钮"),
        ("1.1.5", "· 更新页去掉「再次检查」「查看更新日志」两个按钮"),
        ("1.1.4", "· 移除设置里的「联机」「反馈」页\n· 隐藏更新通道 / 自动更新 / Mirror 酱 CDK"),
        ("1.1.3", "· 主窗口标题改为 Cyrene-Craft-Launcher"),
        ("1.1.2", "· 启动参数里的自定义信息默认值改为 Cyrene-Craft-Launcher"),
        ("1.1.1", "· 更新后首次启动，主界面会显示本次更新内容\n· 版本信息统一收进更新公告里"),
        ("1.1.0", "· 新增「整合包直装工具」：.mrpack 一键变成离线可装\n· 标题栏多了包裹图标"),
        ("1.0.9", "· 微软正版登录开箱可用（内置 OAuth 客户端 ID）\n· 读取顺序：环境变量 → ms-client-id.txt → 内置"),
        ("1.0.8", "· 下载源增加 mediafilez 兜底，单个文件并发 7 → 12\n· 整合包下载失败率明显下降"),
        ("1.0.7", "· 移除 EasyTier 联机，只保留樱花穿透\n· 包体从 109 MB 瘦到 79 MB"),
        ("1.0.6", "· 清理重复的 frpc 进程\n· 隧道连上后会检查本地端口是否在监听"),
        ("1.0.5", "· 修复 frpc 输出乱码（GBK）\n· 记住访问密钥与隧道 ID"),
        ("1.0.4", "· 修复公网地址解析，不再把 127.0.0.1 当结果\n· 隧道窗口加日志区"),
        ("1.0.3", "· 新增樱花穿透"),
        ("1.0.2", "· EasyTier 联机窗口加 DHCP 与中转地址"),
        ("1.0.1", "· 关闭地区限制，下载与补全恢复可用"),
        ("1.0.0", "· 从 PCL-CE 2.15.1 改名，粉色主题、昔涟背景/图标/启动问候\n· 本地更新、离线登录常开")
    ];

    private static string StatePath =>
        Path.Combine(AppContext.BaseDirectory, "PCL", "last-version.txt");

    private static string LastVersion()
    {
        try
        {
            return File.Exists(StatePath) ? File.ReadAllText(StatePath).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    private static void SaveVersion(string version)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, version);
        }
        catch
        {
            // 写不进就算了，下次还会弹
        }
    }

    /// <summary>
    /// 全部版本的改动，从新到旧。
    /// </summary>
    public static string AllNotes()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"当前版本：CCL V{CurrentVersion}");
        sb.AppendLine();
        foreach (var (version, notes) in Entries)
        {
            sb.AppendLine($"【CCL V{version}】");
            sb.AppendLine(notes.Replace("\n", Environment.NewLine));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 有更新时返回公告文本，否则返回 null。
    /// </summary>
    public static string? BuildNotes(string lastVersion)
    {
        if (string.Equals(lastVersion, CurrentVersion, StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(lastVersion))
            // 第一次运行：不弹长文，只留一句
            return "欢迎回来，伙伴。\n\n这是第一次启动这个版本，人家把这次的改动写在这里了。\n";

        var sb = new System.Text.StringBuilder();
        var found = false;
        foreach (var (version, notes) in Entries)
        {
            if (string.Equals(version, lastVersion, StringComparison.OrdinalIgnoreCase)) break;
            sb.AppendLine($"【CCL V{version}】");
            sb.AppendLine(notes);
            sb.AppendLine();
            found = true;
        }

        if (!found) return null;

        sb.AppendLine("——");
        sb.AppendLine("更新包已装好，可以正常玩了。");
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 启动时调用：判断是否需要弹更新公告。
    /// </summary>
    public static void ShowIfUpdated()
    {
        try
        {
            var last = LastVersion();
            var notes = BuildNotes(last);
            if (notes is null)
            {
                SaveVersion(CurrentVersion);
                return;
            }

            CyreneHost.ShowMessage?.Invoke(notes, $"CCL V{CurrentVersion} · 更新内容", null);
            SaveVersion(CurrentVersion);
        }
        catch
        {
            // 公告失败不能影响启动
        }
    }
}
