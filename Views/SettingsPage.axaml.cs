using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DragonDen.ModManager.Services;
using DragonDen.ModManager.Services.Localization;
using DragonDen.ModManager.Storage;

namespace DragonDen.ModManager.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();

        App.ConfigChanged += RefreshFromConfig;

        RefreshFromConfig();

        BrowseSPTBtn.Click += OnBrowseSptSptFolder;
        BrowseDataBtn.Click += OnBrowseDataFolder;
        ResetDataBtn.Click += OnResetDataFolder;
        SaveBtn.Click += OnSave;

        SptRootBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) UpdateComputed();
        };
        DataBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) UpdateComputed();
        };
        ClientRelBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) UpdateComputed();
        };
        ServerRelBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) UpdateComputed();
        };

        ShowTokenToggle.Checked += (_, __) => ToggleTokenVisibility(true);
        ShowTokenToggle.Unchecked += (_, __) => ToggleTokenVisibility(false);

        ClearCacheBtn.Click += OnClearCache;
        ClearTempFilesBtn.Click += OnClearTemp;
        ClearLogFilesBtn.Click += (_,__) => Logger.CleanAllLogs();

        ApplyLanguage();
        Loc.LanguageChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLanguage);
        if (LanguageCombo != null)
            LanguageCombo.SelectionChanged += OnLanguageComboChanged;
    }

    private void OnLanguageComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            Loc.SetLanguage(tag, save: true);
    }

    private void ApplyLanguage()
    {
        try
        {
            if (LblSptFolder != null) LblSptFolder.Text = Loc.T("Settings.SptFolder");
            if (LblSptHint != null) LblSptHint.Text = Loc.T("Settings.SptFolderHint");
            if (BrowseSPTBtn != null) BrowseSPTBtn.Content = Loc.T("Common.Browse");
            if (LblDataFolder != null) LblDataFolder.Text = Loc.T("Settings.DataFolder");
            if (LblDataHint != null) LblDataHint.Text = Loc.T("Settings.DataFolderHint");
            if (LblDataEmpty != null) LblDataEmpty.Text = Loc.T("Settings.DataFolderEmpty");
            if (BrowseDataBtn != null) BrowseDataBtn.Content = Loc.T("Common.Browse");
            if (ResetDataBtn != null) ResetDataBtn.Content = Loc.T("Settings.ResetDefault");
            if (LblModPaths != null) LblModPaths.Text = Loc.T("Settings.ModPaths");
            if (LblClientSub != null) LblClientSub.Text = Loc.T("Settings.ClientSubpath");
            if (LblServerSub != null) LblServerSub.Text = Loc.T("Settings.ServerSubpath");
            if (LblFinalPaths != null) LblFinalPaths.Text = Loc.T("Settings.FinalPaths");
            if (LblForgeToken != null) LblForgeToken.Text = Loc.T("Settings.ForgeToken");
            if (LblForgeHint != null) LblForgeHint.Text = Loc.T("Settings.ForgeTokenHint");
            if (ShowTokenToggle != null) ShowTokenToggle.Content = Loc.T("Common.Show");
            if (LblUI != null) LblUI.Text = Loc.T("Settings.UI");
            if (LblExpertTip != null) LblExpertTip.Text = Loc.T("Settings.ExpertTip");
            if (ExpertModeToggle != null) ExpertModeToggle.Content = Loc.T("Settings.ExpertMode");
            if (LanguageLabel != null) LanguageLabel.Text = Loc.T("Settings.Language");
            if (SaveBtn != null) SaveBtn.Content = Loc.T("Common.Save");
            if (ClearCacheBtn != null) ClearCacheBtn.Content = Loc.T("Settings.ClearCache");
            if (ClearTempFilesBtn != null) ClearTempFilesBtn.Content = Loc.T("Settings.ClearTemp");
            if (ClearLogFilesBtn != null) ClearLogFilesBtn.Content = Loc.T("Settings.ClearLogs");
            // Spt status placeholder if still default
            if (SptStatusText != null && (SptStatusText.Text == "No folder selected." || string.IsNullOrWhiteSpace(SptRootBox?.Text)))
                SptStatusText.Text = Loc.T("Settings.NoFolder");
        }
        catch (System.Exception ex)
        {
            Logger.Error($"[Settings] ApplyLanguage: {ex.Message}");
        }
    }

    private void ToggleTokenVisibility(bool show)
    {
        ForgeTokenBox.PasswordChar = show ? '\0' : '•';
        ShowTokenToggle.Content = show ? "隐藏" : "显示";
    }

    public void RefreshFromConfig()
    {
        SptRootBox.Text = App.Config.Paths.SptRoot ?? "";
        DataBox.Text = App.Config.Paths.DataFolder ?? "";
        ClientRelBox.Text = App.Config.Paths.ClientModsRelative;
        ServerRelBox.Text = App.Config.Paths.ServerModsRelative;
        ForgeTokenBox.Text = App.Config.Forge.Token ?? "";
        ShowTokenToggle.IsChecked = false;
        ToggleTokenVisibility(false);
        
        ExpertModeToggle.IsChecked = App.Config.UI.ExpertMode;

        // Language combo
        try
        {
            var lang = string.IsNullOrWhiteSpace(App.Config.UI.Language)
                ? DragonDen.ModManager.Services.Localization.Loc.CurrentLanguage
                : App.Config.UI.Language;
            foreach (var item in LanguageCombo.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag as string, lang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageCombo.SelectedItem = cbi;
                    break;
                }
            }
            if (LanguageCombo.SelectedItem == null && LanguageCombo.Items.Count > 0)
                LanguageCombo.SelectedIndex = 0;
        }
        catch { /* ignore */ }

        UpdateComputed();
        UpdateSptDetectionStatus();
    }

    private void OnResetDataFolder(object? s, RoutedEventArgs e)
    {
        App.Config.Paths.DataFolder = "";
        DataBox.Text = Paths.DataDir;
        App.SaveConfig();
        Notifications.Current.ShowSuccess("数据目录已重置", "数据目录已恢复为默认位置。");
        Logger.Info("[SettingsPage] Data folder reset to default.");
    }

    private async void OnBrowseDataFolder(object? s, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner?.StorageProvider is null) return;

        var pick = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择新的数据文件夹。"
        });

        if (pick.Count == 0) return;

        var chosen = pick[0].Path.LocalPath;
        if (!Directory.Exists(chosen))
        {
            Notifications.Current.ShowError("无效文件夹", "所选文件夹不存在。");
            Logger.Error("[SettingsPage] Selected data folder doesn't exist: " + chosen);
            return;
        }

        App.Config.Paths.DataFolder = chosen;
        App.SaveConfig();
        Notifications.Current.ShowSuccess("数据目录已更改", "数据目录已成功更新。");
        Logger.Info("[SettingsPage] Data folder changed to: " + chosen);
    }

    private async void OnBrowseSptSptFolder(object? s, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner?.StorageProvider is null) return;

        var pick = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择 SPT 文件夹。"
        });

        if (pick.Count == 0) return;

        var chosen = pick[0].Path.LocalPath;
        if (!Directory.Exists(chosen))
        {
            Notifications.Current.ShowError("无效文件夹", "所选 SPT 文件夹不存在。");
            Logger.Error("[SettingsPage] Selected SPT folder doesn't exist: " + chosen);
            return;
        }

        // Promote SPT_Runtime → parent when BepInEx is a sibling
        var chosenName = Path.GetFileName(chosen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (chosenName.Equals("SPT_Runtime", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(chosen)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(Path.Combine(parent, "BepInEx")))
            {
                chosen = parent!;
                Logger.Info("[SettingsPage] Promoted SPT_Runtime to parent root: " + chosen);
            }
        }

        if (!TryFindSptExe(chosen, out var exePath))
        {
            Notifications.Current.ShowError(
                "无效的 SPT 目录",
                "未找到 SPT.Server.exe。\n请选择含 BepInEx 与 SPT_Runtime 的根目录，或直接选择 SPT_Runtime 文件夹。"
            );
            Logger.Error("[SettingsPage] Invalid SPT folder: missing SPT.Server.exe in " + chosen);
            return;
        }

        var major = 0;
        var friendly = "";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var fv = info?.FileVersion ?? "";
            var parts = fv.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            friendly = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : fv;
        }
        catch (Exception ex)
        {
            Logger.Error("[SettingsPage] Failed to read SPT version: " + ex);
        }

        var clientRel = "BepInEx/plugins";
        if (Directory.Exists(Path.Combine(chosen, "BepInEx", "plugins")))
            clientRel = "BepInEx/plugins";

        // Prefer SPT 4.1+ layout (SPT_Runtime), then legacy SPT, then flat user/mods
        string serverRel = Spt.DetectServerModsRelative(chosen);

        var oldRoot = App.Config.Paths.SptRoot ?? "";
        App.Config.Paths.SptRoot = chosen;
        App.Config.Paths.ClientModsRelative = clientRel;
        App.Config.Paths.ServerModsRelative = serverRel;
        App.SaveConfig();

        if (!string.Equals(oldRoot, chosen, StringComparison.OrdinalIgnoreCase))
            try
            {
                var newDbPath = Paths.ModsDbPath;
                App.Db = new Db(newDbPath);
                App.Db.Init();
                Notifications.Current.ShowSuccess("数据库已切换", $"已切换模组数据库 → {Path.GetFileName(newDbPath)}");
                Logger.Info("[SettingsPage] Mods DB switched to: " + newDbPath);
            }
            catch (Exception ex)
            {
                Notifications.Current.ShowError("数据库错误", "切换模组数据库失败。");
                Logger.Error("[SettingsPage] Failed to switch mods database: " + ex);
            }

        SptRootBox.Text = chosen;
        ClientRelBox.Text = clientRel;
        ServerRelBox.Text = serverRel;
        UpdateComputed();
        UpdateSptDetectionStatus();

        Notifications.Current.ShowSuccess("SPT 目录已保存", string.IsNullOrWhiteSpace(friendly)
            ? "SPT 目录已成功保存。"
            : $"已识别并保存 SPT {friendly}。");
        Logger.Info("[SettingsPage] SPT folder saved and config updated.");

        var main = (MainWindow?)TopLevel.GetTopLevel(this);
        var tabs = main.FindDescendantOfType<TabControl>();
        if (tabs is not null) tabs.SelectedIndex = 1;
        var installed = main?.FindDescendantOfType<InstalledModsPage>();
        _ = installed?.RefreshFromSettingsAsync();
    }

    private static bool TryFindSptExe(string root, out string exePath)
    {
        return Spt.TryFindAnyServerExe(root, out exePath);
    }

    private void OnSave(object? s, RoutedEventArgs e)
    {
        var oldRoot = App.Config.Paths.SptRoot ?? "";

        App.Config.Paths.SptRoot = (SptRootBox.Text ?? "").Trim();
        App.Config.Paths.DataFolder = (DataBox.Text ?? "data").Trim();
        App.Config.Paths.ClientModsRelative = ClientRelBox.Text ?? "BepInEx/plugins";
        App.Config.Paths.ServerModsRelative = ServerRelBox.Text ?? "SPT_Runtime/user/mods";
        App.Config.Forge.Token = (ForgeTokenBox.Text ?? "").Trim();
        App.Config.UI.ExpertMode = ExpertModeToggle.IsChecked == true;

        // Language
        try
        {
            if (LanguageCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                App.Config.UI.Language = tag;
                DragonDen.ModManager.Services.Localization.Loc.SetLanguage(tag, save: false);
            }
        }
        catch { /* ignore */ }

        App.SaveConfig();
        App.RaiseConfigChanged();
        var newRoot = App.Config.Paths.SptRoot ?? "";
        if (!string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase))
            try
            {
                var newDbPath = Paths.ModsDbPath;
                App.Db = new Db(newDbPath);
                App.Db.Init();
                Notifications.Current.ShowSuccess("数据库已切换", $"已切换模组数据库 → {Path.GetFileName(newDbPath)}");
                Logger.Info("[SettingsPage] Mods DB switched to: " + newDbPath);
            }
            catch (Exception ex)
            {
                Notifications.Current.ShowError("数据库错误", "切换模组数据库失败。");
                Logger.Error("[SettingsPage] Failed to switch mods database: " + ex);
            }

        RefreshFromConfig();
        Notifications.Current.ShowSuccess("设置已保存", "所有设置已成功保存。");
        Logger.Info("[SettingsPage] Settings saved and configuration updated.");

        var main = (MainWindow?)TopLevel.GetTopLevel(this);
        var installed = main?.FindDescendantOfType<InstalledModsPage>();
        _ = installed?.RefreshFromSettingsAsync();
        installed?.GetType().GetMethod("RefreshRows", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(installed, null);
    }

    private void UpdateComputed()
    {
        var root = SptRootBox.Text ?? "";
        var client = (ClientRelBox.Text ?? "").Replace('/', Path.DirectorySeparatorChar);
        var server = (ServerRelBox.Text ?? "").Replace('/', Path.DirectorySeparatorChar);

        ClientFullText.Text = string.IsNullOrWhiteSpace(root) ? "" : Path.Combine(root, client);
        ServerFullText.Text = string.IsNullOrWhiteSpace(root) ? "" : Path.Combine(root, server);
    }

    private void UpdateSptDetectionStatus()
    {
        var root = SptRootBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            SptStatusText.Text = "尚未选择文件夹。";
            return;
        }

        if (TryFindSptExe(root, out var exePath))
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                var fv = info?.FileVersion ?? "";
                var parts = fv.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var friendly = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : fv;
                SptStatusText.Text = string.IsNullOrWhiteSpace(friendly)
                    ? "已检测到 SPT（版本未知）"
                    : $"已检测到 SPT {friendly}";
            }
            catch
            {
                SptStatusText.Text = "已检测到 SPT（版本未知）";
            }
        else
            SptStatusText.Text = "在此文件夹中找不到 SPT.Server.exe。";
    }

    private async void OnClearCache(object? sender, RoutedEventArgs e)
    {
        try
        {
            try
            {
                App.Cache?.Close();
            }
            catch (Exception ex)
            {
                Notifications.Current.ShowError("缓存错误", "清除缓存数据库连接失败。");
                Logger.Error("[SettingsPage] Failed to close cache: " + ex);
            }

            var dbPath = Paths.CacheDbPath;
            var targets = new[] { dbPath, dbPath + "-shm", dbPath + "-wal" };
            foreach (var p in targets)
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                }
                catch (Exception ex)
                {
                    Notifications.Current.ShowError("缓存错误", "删除缓存文件失败。");
                    Logger.Error("[SettingsPage] Failed to delete cache file: " + ex);
                }

            try
            {
                App.Cache?.Init();
            }
            catch (Exception ex)
            {
                Notifications.Current.ShowError("缓存错误", "重新初始化缓存数据库失败。");
                Logger.Error("[SettingsPage] Failed to initialize cache: " + ex);
            }

            Notifications.Current.ShowSuccess("缓存已清除", "所有缓存数据已成功清除。");
            Logger.Info("[SettingsPage] Cache cleared successfully.");
        }
        catch (Exception ex)
        {
            Notifications.Current.ShowError("缓存错误", "清除缓存时发生意外错误。");
            Logger.Error("[SettingsPage] Unexpected cache clear error: " + ex);
        }

        var main = (MainWindow?)TopLevel.GetTopLevel(this);
        if (main is not null)
        {
            var tabs = main.FindDescendantOfType<TabControl>();
            if (tabs is not null) tabs.SelectedIndex = 0;
            var browse = main.FindDescendantOfType<BrowseModsPage>();
            if (browse is not null)
                await browse.TriggerRefresh();
        }
    }

    private void OnClearTemp(object? sender, RoutedEventArgs e)
    {
        var baseDir = string.IsNullOrWhiteSpace(App.Config.Paths.DataFolder) ? Paths.DataDir : App.Config.Paths.DataFolder;
        var downloads = Path.Combine(baseDir, "downloads");
        var thumbs = Path.Combine(baseDir, "thumbs");
        var stage = Path.Combine(baseDir, "stage");

        TryDeleteDir(downloads);
        TryDeleteDir(thumbs);
        TryDeleteDir(stage);

        Notifications.Current.ShowSuccess("临时文件已清除", "所有临时文件已删除。");
        Logger.Info("[SettingsPage] Temporary files cleared successfully.");
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                }
                catch (Exception ex)
                {
                    Logger.Error("[SettingsPage] Failed to reset file attributes: " + ex);
                }

            Directory.Delete(dir, true);
            Logger.Info("[SettingsPage] Deleted directory: " + dir);
        }
        catch (Exception ex)
        {
            Logger.Error("[SettingsPage] Failed to delete directory: " + dir + " | " + ex);
        }
    }
}