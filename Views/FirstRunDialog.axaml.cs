using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DragonDen.ModManager.Services;

namespace DragonDen.ModManager.Views;

public partial class FirstRunDialog : Window
{
    public enum Result
    {
        None,
        Select,
        Skip,
        CloseApp
    }

    public FirstRunDialog()
    {
        InitializeComponent();
        SelectBtn.Click += OnSelectAsync;
        CloseBtn.Click += OnSkip;
    }

    private async void OnSelectAsync(object? s, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        if (storage is null)
        {
            Close(Result.Skip);
            return;
        }

        var pick = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择 SPT 根目录 / Select SPT root folder"
        });
        if (pick.Count == 0) return;

        var chosen = pick[0].Path.LocalPath;
        if (!Directory.Exists(chosen))
        {
            Notifications.Current.ShowError("文件夹无效", "所选文件夹不存在。");
            Logger.Warn("[FirstRunDialog] Folder does not exist: " + chosen);
            return;
        }

        // Accept install root (BepInEx + SPT_Runtime) OR the SPT_Runtime folder itself.
        if (!Spt.TryFindAnyServerExe(chosen, out var exe) || string.IsNullOrWhiteSpace(exe))
        {
            VersionText.Text = "无效目录：未找到 SPT.Server.exe。";
            Notifications.Current.ShowError(
                "无效的 SPT 目录",
                "未找到 SPT.Server.exe。\n请选择：\n• 含 BepInEx 与 SPT_Runtime 的安装根目录，或\n• 直接选择 SPT_Runtime 文件夹（其中有 SPT.Server.exe）。"
            );
            Logger.Warn("[FirstRunDialog] SPT.Server.exe not found under: " + chosen);
            return;
        }

        // If user picked SPT_Runtime, promote to parent when sibling BepInEx exists
        var chosenName = Path.GetFileName(chosen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (chosenName.Equals("SPT_Runtime", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(chosen)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(Path.Combine(parent, "BepInEx")))
            {
                chosen = parent!;
                Logger.Info("[FirstRunDialog] Promoted SPT_Runtime selection to parent root: " + chosen);
                Spt.TryFindAnyServerExe(chosen, out exe);
            }
        }

        string threePart, majorTwo;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(exe);
            var raw = (vi.FileVersion ?? "").Trim();
            var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new InvalidOperationException("Unexpected file version: " + raw);
            threePart = parts.Length >= 3
                ? $"{Safe(parts, 0)}.{Safe(parts, 1)}.{Safe(parts, 2)}"
                : $"{Safe(parts, 0)}.{Safe(parts, 1)}.0";
            majorTwo = $"{Safe(parts, 0)}.{Safe(parts, 1)}";
        }
        catch (Exception ex)
        {
            VersionText.Text = "无法读取 SPT 版本信息。";
            Notifications.Current.ShowError("读取失败", "无法从可执行文件读取 SPT 版本，请换一个目录重试。");
            Logger.Error("[FirstRunDialog] Could not read version info: " + ex);
            return;
        }

        App.Config.Paths.SptRoot = chosen;
        App.Config.Paths.ServerModsRelative = Spt.DetectServerModsRelative(chosen);
        if (Directory.Exists(Path.Combine(chosen, "BepInEx", "plugins")))
            App.Config.Paths.ClientModsRelative = "BepInEx/plugins";

        App.SaveConfig();
        App.RaiseConfigChanged();

        VersionText.Text = $"已检测到 SPT {threePart} — 筛选版本 {majorTwo}";
        HintText.Text = exe ?? "";

        Notifications.Current.ShowSuccess("SPT 目录已设置", $"已识别 SPT {threePart} 并保存。");
        Logger.Info("[FirstRunDialog] SPT folder set: " + chosen + " exe=" + exe);
        Close(Result.Select);

        static string Safe(string[] a, int i)
        {
            return i >= 0 && i < a.Length && int.TryParse(a[i], out var n) ? n.ToString() : "0";
        }
    }

    private void OnSkip(object? s, RoutedEventArgs e)
    {
        Close(Result.Skip);
    }
}
