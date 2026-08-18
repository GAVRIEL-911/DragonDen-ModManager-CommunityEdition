using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DragonDen.ModManager.Services;
using DragonDen.ModManager.Services.Localization;
using DragonDen.ModManager.Storage;
using DragonDen.ModManager.Utils;
using Color = Avalonia.Media.Color;
using Size = Avalonia.Size;

namespace DragonDen.ModManager.Views;

public partial class MainWindow : Window
{
    private readonly TextBlock? _footerCenter;
    private readonly TextBlock? _footerLeft;

    public MainWindow()
    {
        InitializeComponent();
        
        Opened += (_, __) => { 
            var scale = this.RenderScaling;
            ClientSize = new Size(1280, 720); 
        };

        _footerLeft = this.FindControl<TextBlock>("FooterLeft");
        _footerCenter = this.FindControl<TextBlock>("FooterCenter");

        ApplyLanguage();
        Loc.LanguageChanged += () => Dispatcher.UIThread.Post(ApplyLanguage);

        App.ConfigChanged += () =>
        {
            var settings = this.FindDescendantOfType<SettingsPage>();
            settings?.RefreshFromConfig();
        };

        Opened += OnOpenedAsync;

        if (App.Queue is not null)
        {
            App.Queue.Jobs.CollectionChanged += (_, __) =>
            {
                AttachJobHandlers();
                UpdateFooterCenter();
            };
            AttachJobHandlers();
        }

        UpdateFooterCenter();
    }

    private async void OnOpenedAsync(object? s, EventArgs e)
    {
        // Token is optional (public read-only API). Prompt once if missing, allow skip.
        if (string.IsNullOrWhiteSpace(App.Config.Forge.Token))
        {
            var dlg = new TokenDialog();
            var res = await dlg.ShowDialog<TokenDialog.Result?>(this) ?? TokenDialog.Result.Skip;
            // Skip / Set both continue; only CloseApp would exit (no longer used by Cancel)
            if (res == TokenDialog.Result.CloseApp)
            {
                // keep compatibility but do not force quit on cancel anymore
            }
        }

        // SPT root optional at startup — allow skip so user can reach Settings
        if (string.IsNullOrWhiteSpace(App.Config.Paths.SptRoot) ||
            !Directory.Exists(App.Config.Paths.SptRoot!))
        {
            var dlg = new FirstRunDialog();
            var res = await dlg.ShowDialog<FirstRunDialog.Result?>(this) ?? FirstRunDialog.Result.Skip;
            if (res == FirstRunDialog.Result.CloseApp)
            {
                // ignore — treat as skip so Settings remains accessible
            }
        }
        
        await SelfUpdateChecker.CheckOnStartupAsync(this);

        try
        {
            var modsDbPath = Paths.ModsDbPath;
            App.Db = new Db(modsDbPath);
            App.Db.Init();
        }
        catch (Exception ex)
        {
            Logger.Error($"[MainWindow] Error initializing mods db: {ex.Message}");
        }

        if (Spt.TryGetServerVersionThree(out var _three, out var majorTwo))
        {
            var installPage = this.FindDescendantOfType<BrowseModsPage>();
            installPage?.SelectSptMajor(majorTwo);
        }

        var year = DateTime.Now.Year;
        _footerLeft!.Text = $"© {year} Dragon Den Mod Manager";

        UpdateFooterCenter();
    }

    private void OnOpenKoFi(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/drexira",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Notifications.Current.ShowError("打开失败", "无法打开 Ko-fi 页面，请检查网络或默认浏览器。");
            Logger.Error($"[MainWindow] Failed to open Ko-Fi link: {ex.Message}");
        }
    }

    private void AttachJobHandlers()
    {
        if (App.Queue?.Jobs is null) return;
        foreach (var j in App.Queue.Jobs)
        {
            j.PropertyChanged -= OnAnyJobPropertyChanged;
            j.PropertyChanged += OnAnyJobPropertyChanged;
        }
    }

    private void OnAnyJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallJob.IsIndeterminate) ||
            e.PropertyName == nameof(InstallJob.Progress) ||
            e.PropertyName == nameof(InstallJob.Status) ||
            e.PropertyName == "IsCompleted" ||
            e.PropertyName == nameof(InstallJob.Phase))
            UpdateFooterCenter();
    }

    private void UpdateFooterCenter()
    {
        if (_footerCenter is null) return;

        var total = App.Queue?.Jobs.Count ?? 0;
        var completed = App.Queue?.Jobs.Count(j => j.IsCompleted) ?? 0;

        if (total == 0 || completed == total)
        {
            _footerCenter.Text = "安装队列";
            _footerCenter.Foreground = this.FindResource("Dd.LightGrey") as IBrush ?? _footerCenter.Foreground;
            _footerCenter.HorizontalAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(_footerCenter, "打开安装队列");
            return;
        }

        _footerCenter.Text = $"安装队列 — {completed}/{total}";
        _footerCenter.Foreground = new SolidColorBrush(Color.Parse("#FF8A00"));
        ToolTip.SetTip(_footerCenter, "点击查看进度");
    }

    private async void OnFooterCenterClick(object? sender, PointerPressedEventArgs e)
    {
        var existing = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.Windows.OfType<InstallationQueueDialog>().FirstOrDefault();

        try
        {
            if (existing is not null)
                existing.Close();
        }
        catch (Exception ex)
        {
            Logger.Error($"[MainWindow] Error closing existing InstallationQueueDialog: {ex.Message}");
        }

        var dlg = new InstallationQueueDialog
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            CanResize = true
        };
        dlg.Show(this);
    }

    private void ApplyLanguage()
    {
        try
        {
            Title = Loc.T("App.Title") + " - Community Edition v0.0.8.2C";
            if (TabBrowse != null) TabBrowse.Header = Loc.T("Nav.Browse");
            if (TabInstalled != null) TabInstalled.Header = Loc.T("Nav.Installed");
            if (TabCollections != null) TabCollections.Header = Loc.T("Nav.Collections");
            if (TabCollectionsProfiles != null) TabCollectionsProfiles.Header = Loc.T("Nav.CollectionsProfiles");
            if (TabSettings != null) TabSettings.Header = Loc.T("Nav.Settings");
        }
        catch (Exception ex)
        {
            Logger.Error($"[MainWindow] ApplyLanguage failed: {ex.Message}");
        }
    }

}