using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DragonDen.ModManager.Services;

public static class Spt
{
    public static string? Root => App.Config.Paths.SptRoot;
    public static string ClientModsPath => string.IsNullOrWhiteSpace(Root) ? "" : Path.Combine(Root!, Normalize(App.Config.Paths.ClientModsRelative));
    public static string ServerModsPath => string.IsNullOrWhiteSpace(Root) ? "" : Path.Combine(Root!, Normalize(App.Config.Paths.ServerModsRelative));

    private static string Normalize(string p)
    {
        return p.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Possible relative roots for server mods (SPT 4.1+ uses SPT_Runtime, older builds used SPT).
    /// </summary>
    public static IEnumerable<string> ServerModRelativeRoots()
    {
        yield return "SPT_Runtime/user/mods";
        yield return "SPT/user/mods";
        yield return "user/mods";
    }

    /// <summary>
    /// Absolute paths that may contain server mods for the current SPT root.
    /// </summary>
    public static IEnumerable<string> EnumerateServerModDirs()
    {
        var root = Root ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            yield break;

        foreach (var rel in ServerModRelativeRoots())
        {
            var abs = Path.Combine(root, Normalize(rel));
            if (Directory.Exists(abs))
                yield return abs;
        }
    }

    /// <summary>
    /// Pick the best ServerModsRelative for config based on what exists on disk.
    /// Prefer SPT_Runtime (4.1+), fall back to SPT, then user/mods.
    /// </summary>
    public static string DetectServerModsRelative(string? sptRoot = null)
    {
        var root = sptRoot ?? Root ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return "SPT_Runtime/user/mods";

        if (Directory.Exists(Path.Combine(root, "SPT_Runtime", "user", "mods"))
            || Directory.Exists(Path.Combine(root, "SPT_Runtime")))
            return "SPT_Runtime/user/mods";

        if (Directory.Exists(Path.Combine(root, "SPT", "user", "mods"))
            || Directory.Exists(Path.Combine(root, "SPT")))
            return "SPT/user/mods";

        if (Directory.Exists(Path.Combine(root, "user", "mods")))
            return "user/mods";

        return "SPT_Runtime/user/mods";
    }

    public static bool TryFindAnyServerExe(out string exePath)
        => TryFindAnyServerExe(Root, out exePath);

    public static bool TryFindAnyServerExe(string? root, out string exePath)
    {
        exePath = "";
        try
        {
            root ??= "";
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;

            // SPT 4.1+ often places the server under SPT_Runtime
            var candidates = new[]
            {
                Path.Combine(root, "SPT.Server.exe"),
                Path.Combine(root, "SPT_Runtime", "SPT.Server.exe"),
                Path.Combine(root, "SPT_Runtime", "Server.exe"),
                Path.Combine(root, "SPT", "SPT.Server.exe"),
                Path.Combine(root, "SPT", "Server.exe"),
                Path.Combine(root, "Aki.Server.exe"),
                Path.Combine(root, "Aki.Server", "Aki.Server.exe"),
                Path.Combine(root, "Server", "Server.exe"),
                Path.Combine(root, "Server.exe"),
            };

            foreach (var p in candidates)
                if (File.Exists(p))
                {
                    exePath = p;
                    return true;
                }

            var extra = new List<string>();
            try
            {
                extra.AddRange(Directory.EnumerateFiles(root, "*Server*.exe", SearchOption.TopDirectoryOnly));
            }
            catch (Exception ex)
            {
                Logger.Error($"[Spt] Error enumerating files in root: {ex}");
            }

            foreach (var sub in new[] { "SPT_Runtime", "SPT", "Server", "Aki.Server" })
            {
                var dir = Path.Combine(root, sub);
                if (!Directory.Exists(dir)) continue;
                try
                {
                    extra.AddRange(Directory.EnumerateFiles(dir, "*Server*.exe", SearchOption.TopDirectoryOnly));
                    // also common: just Server.exe / SPT.Server.exe
                    foreach (var name in new[] { "SPT.Server.exe", "Server.exe", "Aki.Server.exe" })
                    {
                        var f = Path.Combine(dir, name);
                        if (File.Exists(f)) extra.Add(f);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Spt] Error enumerating files in {sub}: {ex}");
                }
            }

            var hit = extra.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(hit))
            {
                exePath = hit;
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Spt] Error finding server exe: {ex}");
        }

        return false;
    }

    public static bool TryGetServerVersionThree(out string threePart, out string majorTwo)
    {
        threePart = "";
        majorTwo = "";

        try
        {
            if (!TryFindAnyServerExe(out var exe)) return false;

            var vi = FileVersionInfo.GetVersionInfo(exe);
            var raw = (vi.FileVersion ?? "").Trim();
            var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            var a = Safe(parts, 0);
            var b = Safe(parts, 1);
            var c = parts.Length >= 3 ? Safe(parts, 2) : "0";

            threePart = $"{a}.{b}.{c}";
            majorTwo = $"{a}.{b}";
            return true;
        }
        catch
        {
            return false;
        }

        static string Safe(string[] a, int i)
        {
            return i >= 0 && i < a.Length && int.TryParse(a[i], out var n) ? n.ToString() : "0";
        }
    }
}
