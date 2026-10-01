using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;
using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SelectAI.UI.Tray;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ISettingsService _settingsService;
    private readonly Action _startSelectionAction;
    private readonly Action _openSettingsAction;

    public TrayIconManager(
        ISettingsService settingsService,
        Action startSelectionAction,
        Action openSettingsAction)
    {
        _settingsService = settingsService;
        _startSelectionAction = startSelectionAction;
        _openSettingsAction = openSettingsAction;

        _notifyIcon = new NotifyIcon
        {
            Text = "SelectAI — Screen Selection & AI Assistant",
            Visible = true,
            Icon = CreateAppIcon()
        };

        _notifyIcon.DoubleClick += (s, e) => _startSelectionAction();
        BuildContextMenu();
    }

    public void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Title Header
        var titleItem = new ToolStripMenuItem("SelectAI")
        {
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
            Enabled = false
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new ToolStripSeparator());

        // Start Selection
        var shortcutText = $"{_settingsService.CurrentSettings.HotkeyModifiers} + {_settingsService.CurrentSettings.HotkeyKey}";
        var startItem = new ToolStripMenuItem($"Start Selection ({shortcutText})", null, (s, e) => _startSelectionAction());
        startItem.Font = new Font(menu.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(startItem);

        menu.Items.Add(new ToolStripSeparator());

        // Settings
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (s, e) => _openSettingsAction()));

        // AI Provider Submenu
        var aiMenu = new ToolStripMenuItem("AI Provider");
        var mockItem = new ToolStripMenuItem("SelectAI Built-in Assistant", null, (s, e) => SetAiProvider(AiProviderType.Mock));
        var geminiItem = new ToolStripMenuItem("Google Gemini", null, (s, e) => SetAiProvider(AiProviderType.Gemini));
        var openAiItem = new ToolStripMenuItem("OpenAI (ChatGPT)", null, (s, e) => SetAiProvider(AiProviderType.OpenAi));

        mockItem.Checked = _settingsService.CurrentSettings.ActiveAiProvider == AiProviderType.Mock;
        geminiItem.Checked = _settingsService.CurrentSettings.ActiveAiProvider == AiProviderType.Gemini;
        openAiItem.Checked = _settingsService.CurrentSettings.ActiveAiProvider == AiProviderType.OpenAi;

        aiMenu.DropDownItems.Add(mockItem);
        aiMenu.DropDownItems.Add(geminiItem);
        aiMenu.DropDownItems.Add(openAiItem);
        menu.Items.Add(aiMenu);

        // Search Provider Submenu
        var searchMenu = new ToolStripMenuItem("Search Provider");
        var googleSearchItem = new ToolStripMenuItem("Google Search (Default)", null, (s, e) => { }) { Checked = true };
        var googleLensItem = new ToolStripMenuItem("Google Lens", null, (s, e) => { }) { Checked = true };
        searchMenu.DropDownItems.Add(googleSearchItem);
        searchMenu.DropDownItems.Add(googleLensItem);
        menu.Items.Add(searchMenu);

        // Keyboard Shortcut info
        menu.Items.Add(new ToolStripMenuItem($"Shortcut: {shortcutText}", null, (s, e) => _openSettingsAction()));

        menu.Items.Add(new ToolStripSeparator());

        // Check for Updates
        menu.Items.Add(new ToolStripMenuItem("Check for Updates...", null, async (s, e) =>
        {
            var update = await Core.Services.UpdateCheckerService.CheckForUpdateAsync();
            if (update.UpdateAvailable)
            {
                var res = MessageBox.Show(
                    $"A new version ({update.LatestVersion}) of SelectAI is available!\n\nWould you like to open the download page?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = update.DownloadUrl,
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                MessageBox.Show(
                    $"You are using the latest version of SelectAI ({Core.Services.UpdateCheckerService.CurrentVersion}).",
                    "SelectAI is Up to Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }));

        // About
        menu.Items.Add(new ToolStripMenuItem("About SelectAI", null, (s, e) => ShowAboutDialog()));

        // Exit
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }));

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void SetAiProvider(AiProviderType provider)
    {
        _settingsService.CurrentSettings.ActiveAiProvider = provider;
        _settingsService.SaveSettings();
        BuildContextMenu();
    }

    private void ShowAboutDialog()
    {
        MessageBox.Show(
            "SelectAI — Customizable alternative to Galaxy Book AI Select\n\n" +
            "• Freeform circle tracing with glowing real-time animations\n" +
            "• Offline Windows Native OCR\n" +
            "• Google Search & Google Lens integration\n" +
            "• Multimodal AI (Gemini & OpenAI) provider support\n" +
            "• 100% Local processing privacy guarantee\n\n" +
            "Press Ctrl + Shift + Space anytime to start.",
            "About SelectAI",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Draw glowing cyan circle
            using var glowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 0, 212, 255), 4);
            g.DrawEllipse(glowPen, 4, 4, 24, 24);

            using var corePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 0, 212, 255), 2);
            g.DrawEllipse(corePen, 6, 6, 20, 20);

            // Sparkle in center
            using var brush = new SolidBrush(System.Drawing.Color.White);
            g.FillEllipse(brush, 14, 14, 4, 4);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
