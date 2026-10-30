using PCL.Cyrene;
using System.Windows;
using PCL.Cyrene;
using System.Windows.Controls;
using PCL.Cyrene;
using System.Windows.Input;
using PCL.Cyrene;
using PCL.Core.App;
using PCL.Cyrene;
using PCL.Core.Utils;
using PCL.Cyrene;
using PCL.Core.App.Localization;
using PCL.Cyrene;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class PageSetupUpdate
{
    public VersionDataModel updateInfo;

    /// <summary>
    /// 从本地 zip 更新包覆盖安装：启动一个尾巴脚本，等自己退出后解压、再启动。
    /// </summary>
    private void BtnLocalUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 PCL 更新包",
            Filter = "压缩包 (*.zip)|*.zip"
        };
        if (dialog.ShowDialog() != true) return;

        var zip = dialog.FileName;
        var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        var current = System.Diagnostics.Process.GetCurrentProcess();
        var pid = current.Id;
        var exe = current.MainModule?.FileName
                  ?? System.IO.Path.Combine(appDir, "Cyrene Craft Launcher.exe");

        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cyrene-update.log");
        var script = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-local-update.ps1");
        var content =
            "$ErrorActionPreference = 'Continue'\r\n" +
            $"\"start $(Get-Date -Format o) wait pid={pid}\" | Out-File -FilePath '{logPath}' -Encoding utf8\r\n" +
            "Start-Sleep -Seconds 2\r\n" +
            $"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 400 }}\r\n" +
            $"\"closed $(Get-Date -Format o)\" | Out-File -FilePath '{logPath}' -Append -Encoding utf8\r\n" +
            $"Expand-Archive -LiteralPath '{zip}' -DestinationPath '{appDir}' -Force -ErrorAction Continue 2>&1 | Out-File -FilePath '{logPath}' -Append -Encoding utf8\r\n" +
            $"\"extracted $(Get-Date -Format o)\" | Out-File -FilePath '{logPath}' -Append -Encoding utf8\r\n" +
            $"Start-Process '{exe}'\r\n";
        System.IO.File.WriteAllText(script, content, new System.Text.UTF8Encoding(true));

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        ModMain.frmMain.EndProgram(false, true);
    }

    public PageSetupUpdate()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        ModAnimation.AniBeginControl();
        TextMirrorCDK.Password = Config.Update.MirrorChyanKey;

        ComboSystemUpdateChannel.SelectedIndex = (int)Config.Update.UpdateChannel;
        ComboSystemUpdateMode.SelectedIndex = (int)Config.Update.UpdateMode;

        TextCurrentVersion.Text = "CCL V" + ChangeLog.CurrentVersion;
        ModAnimation.AniEndControl();
        CheckUpdate();
    }

    private async Task<UpdateStatus> IsLatestAsync()
    {
        try
        {
            // 修复：使用 dynamic 绕过命名空间重名导致的编译期类型冲突，
            // 或者你可以尝试替换为 PCL.Core.App.SemVer.Parse(ModBase.versionBaseName)
            if (await UpdateManager.remoteServer.IsLatestAsync(
                    UpdateManager.IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(ModBase.versionBaseName),
                    ModBase.versionCode))
            {
                ModBase.Log("[Update] 已是最新版本");
                return UpdateStatus.Latest;
            }

            ModBase.Log("[Update] 有可用的新版本");
            return UpdateStatus.Available;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Update.Error.NetworkFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Update.Error.NetworkFailed"));
            return UpdateStatus.Error;
        }
    }

    public async void CheckUpdate()
    {
        if (UpdateManager.LocalUpdateOnly)
        {
            CardUpdate.Visibility = Visibility.Collapsed;
            CardCheck.Visibility = Visibility.Visible;
            TextCurrentDesc.Text = "已是最新版本（本地更新模式）";
            BtnCheckAgain.IsEnabled = true;
            return;
        }
        ModBase.Log("[Update] 开始检查更新");
        CardUpdate.Visibility = Visibility.Collapsed;
        CardCheck.Visibility = Visibility.Visible;
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Checking");
        BtnCheckAgain.IsEnabled = false;
        switch (await IsLatestAsync())
        {
            case UpdateStatus.Available:
            {
                Exception checkUpdateEx = null;
                try
                {
                    updateInfo = UpdateManager.remoteServer.GetLatestVersion(
                        UpdateManager.IsCurrentVersionBeta
                            ? UpdateChannel.beta
                            : UpdateChannel.stable, SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
                    TextUpdateName.Text = "PCL CE " + VersionNameFormat(updateInfo.VersionName);
                    var summary = updateInfo.Changelog.Between("<summary>", "</summary>");
                    if (!updateInfo.Changelog.Contains("<summary>") || string.IsNullOrWhiteSpace(summary.Trim()))
                        TextChangelog.Text = Lang.Text("Setup.Update.Changelog.Empty");
                    else
                        TextChangelog.Text = summary;
                }
                catch (Exception ex)
                {
                    checkUpdateEx = ex;
                }

                BtnCheckAgain.IsEnabled = true;
                if (updateInfo is null)
                {
                    TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
                    if (checkUpdateEx is not null)
                        ModBase.Log(
                            checkUpdateEx,
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    else
                        ModBase.Log(
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    return;
                }

                if (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
                {
                    BtnUpdate_Timer();
                    BtnUpdate.IsEnabled = false;
                }
                else if (UpdateManager.isUpdateWaitingRestart)
                {
                    BtnUpdate.Text = Lang.Text("Setup.Update.RestartInstall");
                    BtnUpdate.IsEnabled = true;
                }
                else
                {
                    BtnUpdate.Text = Lang.Text("Setup.Update.Install");
                    BtnUpdate.IsEnabled = true;
                }

                CardUpdate.Visibility = Visibility.Visible;
                CardCheck.Visibility = Visibility.Collapsed;
                break;
            }
            case UpdateStatus.Latest:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.Latest");
                break;
            }
            case UpdateStatus.Error:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
                break;
            }
        }
    }

    public void BtnUpdate_Timer()
    {
        while (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
        {
            ModBase.RunInUi(() => BtnUpdate.Text = Lang.Number(UpdateManager.updateLoader.Progress, "P2"));
            Thread.Sleep(200);
        }
    }

    private void BtnUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        if (UpdateManager.isUpdateWaitingRestart) UpdateManager.UpdateRestart(true);
        // 开始更新流程
        UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);
    }

    private void BtnChangelogDetail_Click(object sender, EventArgs e)
    {
        if (updateInfo is null)
            ModMain.MyMsgBox(Lang.Text("Setup.Update.Changelog.Unavailable"), Lang.Text("Setup.Update.Changelog.Title"));
        else
            ModMain.MyMsgBoxMarkdown(updateInfo.Changelog, Lang.Text("Setup.Update.Changelog.Title"));
    }

    private void ComboSystemUpdateMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ModAnimation.AniExitsAnyControl())
            Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)ComboSystemUpdateMode.SelectedIndex;
    }

    private void ComboSystemUpdateBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniExitsAnyControl())
            return;

        var isCancelled = false;
        switch (ComboSystemUpdateChannel.SelectedIndex)
        {
            case 0:
            {
                break;
            }
            case 1:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                    isCancelled = true;
                else
                    CheckUpdate();
                break;
            }
            case 2:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Update.Channel.Dev.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                {
                    isCancelled = true;
                    break;
                }

                var confirmText = Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.ExpectedInput");
                var ret = ModMain.MyMsgBoxInput(
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Title"),
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Message", confirmText),
                    button1: Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Submit"),
                    button2: Lang.Text("Common.Action.Cancel"), isWarn: true);
    
                if (ret == confirmText)
                {
                    CheckUpdate();
                }
                else
                {
                    HintService.Hint(Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.WrongInput"));
                    isCancelled = true;
                }
                break;
            }
        }

        if (isCancelled)
        {
            ModAnimation.AniBeginControl();
            ComboSystemUpdateChannel.SelectedItem = e.RemovedItems[0];
            ModAnimation.AniEndControl();
        }
        else
        {
            Config.Update.UpdateChannel = (Core.App.UpdateChannel)ComboSystemUpdateChannel.SelectedIndex;
        }
    }

    private void TextMirrorCDK_PasswordChanged(object sender, EventArgs e)
    {
        Config.Update.MirrorChyanKey = TextMirrorCDK.Password;
    }

    private void BtnGetMirrorCDK_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://mirrorchyan.com/");
    }

    private void BtnChangelog_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/v" + ModBase.versionBaseName);
    }

    public string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains("-"))
            return str;
        var add = str.AfterLast("-");
        str = str.BeforeLast("-");
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }

    private void BtnCheckAgain_OnClick(object sender, MouseButtonEventArgs e)
    {
        CheckUpdate();
    }

    private enum UpdateStatus
    {
        Checking = 0,
        Available = 1,
        Error = 2,
        Latest = 3
    }

    private void BtnHistory_Click(object sender, MouseButtonEventArgs e)
    {
        var window = new WindowHistory { };
        window.ShowDialog();
    }

    private void BtnCheckSite_Click(object sender, MouseButtonEventArgs e)
    {
        // 优先用 PCL\site-url.txt 里配置的地址；没有就打开本地官网
        var urlFile = System.IO.Path.Combine(AppContext.BaseDirectory, "PCL", "site-url.txt");
        var url = "";
        try { if (System.IO.File.Exists(urlFile)) url = System.IO.File.ReadAllText(urlFile).Trim(); } catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                TextSiteStatus.Text = "已打开：" + url;
                return;
            }

            var local = @"I:\CCL\website\index.html";
            if (System.IO.File.Exists(local))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(local) { UseShellExecute = true });
                TextSiteStatus.Text = "官网还没上线，先打开了本地页面。上线后把地址写进 PCL\\site-url.txt 就会跳过去了。";
                return;
            }

            TextSiteStatus.Text = "还没有官网地址。把网址写进 PCL\\site-url.txt 即可启用。";
        }
        catch (Exception ex)
        {
            TextSiteStatus.Text = "打开失败：" + ex.Message;
        }
    }}
