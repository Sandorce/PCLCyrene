namespace PCL.Cyrene;

/// <summary>
/// 昔涟定制模块的入口占位。
/// 以后 WindowFrp / WindowPackTool / WindowCrash 这些会陆续搬进来，
/// 主工程只保留一行调用，升级上游时不用再一行行对齐。
/// </summary>
public static class CyreneModule
{
    /// <summary>模块名，用于日志与关于页。</summary>
    public const string Name = "PCL.Cyrene";

    /// <summary>模块版本，跟启动器主版本同步。</summary>
    public const string Version = "1.7.0";
}
