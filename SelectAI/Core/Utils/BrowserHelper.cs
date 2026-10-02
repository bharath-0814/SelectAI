using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SelectAI.Core.Utils;

public record BrowserInfo(string Id, string Name, string? ExecutablePath, bool IsInstalled);

public static class BrowserHelper
{
    private static readonly List<BrowserInfo> _customBrowsers = new();

    public static IReadOnlyList<BrowserInfo> GetAvailableBrowsers()
    {
        var list = new List<BrowserInfo>();

        // 1. Google Chrome
        string? chromePath = FindBrowserPath("chrome.exe", 
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"));
        list.Add(new BrowserInfo("chrome", "Chrome", chromePath, chromePath != null));

        // 2. Brave Browser
        string? bravePath = FindBrowserPath("brave.exe",
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\Application\brave.exe"));
        list.Add(new BrowserInfo("brave", "Brave", bravePath, bravePath != null));

        // 3. Mozilla Firefox
        string? firefoxPath = FindBrowserPath("firefox.exe",
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe");
        list.Add(new BrowserInfo("firefox", "Firefox", firefoxPath, firefoxPath != null));

        // 4. Microsoft Edge
        string? edgePath = FindBrowserPath("msedge.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe");
        list.Add(new BrowserInfo("edge", "Edge", edgePath, edgePath != null));

        // 5. Add custom browsers
        list.AddRange(_customBrowsers);

        return list;
    }

    public static void AddCustomBrowser(string name, string exePath)
    {
        if (File.Exists(exePath))
        {
            var id = "custom_" + Guid.NewGuid().ToString("N")[..6];
            _customBrowsers.Add(new BrowserInfo(id, name, exePath, true));
        }
    }

    public static void OpenUrl(string url, string? browserId = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var browsers = GetAvailableBrowsers();
            var target = browsers.FirstOrDefault(b => b.Id.Equals(browserId, StringComparison.OrdinalIgnoreCase) && b.IsInstalled);

            if (target?.ExecutablePath != null && File.Exists(target.ExecutablePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target.ExecutablePath,
                    Arguments = $"\"{url}\"",
                    UseShellExecute = false
                });
                return;
            }

            // Fallback: system default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to open URL '{url}' in browser: {browserId}", ex);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private static string? FindBrowserPath(string exeName, params string[] fallbackPaths)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
            if (key != null)
            {
                var val = key.GetValue(null)?.ToString();
                if (!string.IsNullOrEmpty(val) && File.Exists(val))
                {
                    return val;
                }
            }
        }
        catch { }

        foreach (var path in fallbackPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
