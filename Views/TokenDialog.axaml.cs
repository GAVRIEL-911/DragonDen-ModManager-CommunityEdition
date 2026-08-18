using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DragonDen.ModManager.Services;

namespace DragonDen.ModManager.Views;

public partial class TokenDialog : Window
{
    public enum Result
    {
        None,
        Set,
        Skip,
        CloseApp
    }

    public TokenDialog()
    {
        InitializeComponent();
        SetBtn.Click += OnSetAsync;
        // Cancel / close = skip, do not force-quit the app
        CloseBtn.Click += (_, __) => Close(Result.Skip);
    }

    private async void OnSetAsync(object? s, RoutedEventArgs e)
    {
        var token = (TokenBox.Text ?? "").Trim();
        // Forge public API is read-only without auth; token is optional.
        // Empty token → skip and continue.
        if (string.IsNullOrWhiteSpace(token))
        {
            Notifications.Current.ShowWarning("未填写令牌", "未填写 API 令牌，将以匿名方式访问 Forge（只读）。可稍后在设置中填写。");
            Logger.Info("[TokenDialog] Empty token — continuing anonymously.");
            Close(Result.Skip);
            return;
        }

        SetBtn.IsEnabled = false;
        CloseBtn.IsEnabled = false;
        SetBtn.Content = "验证中…";

        var apiUp = await CheckApiHealthAsync();
        var tokenOk = apiUp && await CheckTokenValidAsync(token);
        SetBtn.Content = "保存令牌";
        SetBtn.IsEnabled = true;
        CloseBtn.IsEnabled = true;

        if (!apiUp)
        {
            Notifications.Current.ShowError("连接失败", "无法连接 Forge API，请稍后再试。");
            Logger.Error("[TokenDialog] Forge API unreachable during validation.");
            return;
        }

        if (!tokenOk)
        {
            Notifications.Current.ShowError("令牌无效", "令牌验证失败，请确认是有效的只读令牌。");
            Logger.Error("[TokenDialog] Provided Forge token was invalid or rejected.");
            return;
        }

        App.Config.Forge.Token = token;
        App.SaveConfig();
        App.RaiseConfigChanged();
        Notifications.Current.ShowSuccess("令牌已保存", "Forge API 令牌已成功保存。");
        Logger.Info("[TokenDialog] Forge token saved and config updated.");
        Close(Result.Set);
    }

    private static async Task<bool> CheckApiHealthAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var baseUrl = (App.Config?.Forge?.BaseUrl ?? "https://sp-mod.com").TrimEnd('/');
            using var resp = await http.GetAsync($"{baseUrl}/api/v0/ping");
            if (!resp.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean()
                                                                        && doc.RootElement.TryGetProperty("data", out var d)
                                                                        && d.TryGetProperty("message", out var m)
                                                                        && string.Equals(m.GetString(), "pong", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Error("[TokenDialog] API health check failed: " + ex);
            return false;
        }
    }

    private static async Task<bool> CheckTokenValidAsync(string token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var baseUrl = (App.Config?.Forge?.BaseUrl ?? "https://sp-mod.com").TrimEnd('/');
            using var resp = await http.GetAsync($"{baseUrl}/api/v0/mods?per_page=1&page=1");
            if (resp.StatusCode == HttpStatusCode.Unauthorized ||
                resp.StatusCode == HttpStatusCode.Forbidden)
                return false;

            if (!resp.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("success", out var s) || !s.GetBoolean())
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("[TokenDialog] Token validation failed: " + ex);
            return false;
        }
    }

    private void OnOpenTokenHelp(object? s, RoutedEventArgs e)
    {
        try
        {
            var baseUrl = (App.Config?.Forge?.BaseUrl ?? "https://sp-mod.com").TrimEnd('/');
            var url = $"{baseUrl}/user/api-tokens";
            _ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Notifications.Current.ShowError("打开失败", "无法在浏览器中打开令牌帮助页面。");
            Logger.Error("[TokenDialog] Failed to open token help page: " + ex);
        }
    }

    private void OnShowToggle(object? s, RoutedEventArgs e)
    {
        var show = ShowToggle.IsChecked == true;
        TokenBox.PasswordChar = show ? '\0' : '•';
        ShowToggle.Content = show ? "隐藏" : "显示";
    }
}