using System.Windows;
using System.Windows.Controls;
using SelectAI.Core.Enums;
using SelectAI.Core.Interfaces;
using SelectAI.Hotkeys;

namespace SelectAI.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly GlobalHotkeyManager? _hotkeyManager;

    public SettingsWindow(ISettingsService settingsService, GlobalHotkeyManager? hotkeyManager = null)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;

        LoadSettingsToUi();
    }

    private void LoadSettingsToUi()
    {
        var settings = _settingsService.CurrentSettings;

        // General
        ChkStartWithWindows.IsChecked = _settingsService.IsAutoStartEnabled();
        CmbDefaultMode.SelectedIndex = (int)settings.DefaultMode;
        ChkAutoCopyOcr.IsChecked = settings.CopyOcrAutomatically;
        ChkGlowAnimation.IsChecked = settings.EnableGlowAnimation;

        // Shortcut
        ChkCtrl.IsChecked = settings.HotkeyModifiers.Contains("Control", StringComparison.OrdinalIgnoreCase);
        ChkShift.IsChecked = settings.HotkeyModifiers.Contains("Shift", StringComparison.OrdinalIgnoreCase);
        ChkAlt.IsChecked = settings.HotkeyModifiers.Contains("Alt", StringComparison.OrdinalIgnoreCase);
        ChkWin.IsChecked = settings.HotkeyModifiers.Contains("Win", StringComparison.OrdinalIgnoreCase);

        foreach (ComboBoxItem item in CmbHotkeyKey.Items)
        {
            if (string.Equals(item.Content?.ToString(), settings.HotkeyKey, StringComparison.OrdinalIgnoreCase))
            {
                CmbHotkeyKey.SelectedItem = item;
                break;
            }
        }

        // AI Provider
        CmbAiProvider.SelectedIndex = settings.ActiveAiProvider switch
        {
            AiProviderType.Gemini => 1,
            AiProviderType.OpenAi => 2,
            _ => 0
        };

        // Load encrypted API keys safely
        var geminiKey = _settingsService.GetSecret("GeminiApiKey");
        if (!string.IsNullOrEmpty(geminiKey))
        {
            TxtGeminiApiKey.Password = geminiKey;
        }

        var openAiKey = _settingsService.GetSecret("OpenAiApiKey");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            TxtOpenAiApiKey.Password = openAiKey;
        }

        UpdateAiVisibility();
    }

    private void OnAiProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAiVisibility();
    }

    private void UpdateAiVisibility()
    {
        if (PnlGeminiSettings == null || PnlOpenAiSettings == null) return;

        PnlGeminiSettings.Visibility = (CmbAiProvider.SelectedIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
        PnlOpenAiSettings.Visibility = (CmbAiProvider.SelectedIndex == 2) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.CurrentSettings;

        // General
        settings.DefaultMode = (SelectAI.Core.Enums.SelectionMode)CmbDefaultMode.SelectedIndex;
        settings.CopyOcrAutomatically = ChkAutoCopyOcr.IsChecked == true;
        settings.EnableGlowAnimation = ChkGlowAnimation.IsChecked == true;

        bool autoStart = ChkStartWithWindows.IsChecked == true;
        _settingsService.SetAutoStart(autoStart);

        // Hotkey
        var mods = new List<string>();
        if (ChkCtrl.IsChecked == true) mods.Add("Control");
        if (ChkShift.IsChecked == true) mods.Add("Shift");
        if (ChkAlt.IsChecked == true) mods.Add("Alt");
        if (ChkWin.IsChecked == true) mods.Add("Win");
        settings.HotkeyModifiers = string.Join(", ", mods);

        if (CmbHotkeyKey.SelectedItem is ComboBoxItem keyItem && keyItem.Content is string keyStr)
        {
            settings.HotkeyKey = keyStr;
        }

        // AI Provider
        settings.ActiveAiProvider = CmbAiProvider.SelectedIndex switch
        {
            1 => AiProviderType.Gemini,
            2 => AiProviderType.OpenAi,
            _ => AiProviderType.Mock
        };

        if (CmbGeminiModel.SelectedItem is ComboBoxItem gModelItem && gModelItem.Content is string gModel)
        {
            settings.GeminiModel = gModel;
        }

        if (CmbOpenAiModel.SelectedItem is ComboBoxItem oModelItem && oModelItem.Content is string oModel)
        {
            settings.OpenAiModel = oModel;
        }

        // Save secrets encrypted
        _settingsService.SetSecret("GeminiApiKey", TxtGeminiApiKey.Password);
        _settingsService.SetSecret("OpenAiApiKey", TxtOpenAiApiKey.Password);

        _settingsService.SaveSettings();

        // Update active hotkey registration
        if (_hotkeyManager != null)
        {
            bool ok = _hotkeyManager.Register(settings.HotkeyModifiers, settings.HotkeyKey);
            TxtHotkeyStatus.Text = ok
                ? $"Active: {settings.HotkeyModifiers} + {settings.HotkeyKey}"
                : "⚠️ Failed to register hotkey (already in use by another app)";
        }

        TxtSaveFeedback.Text = "Settings saved & applied!";
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _settingsService.ResetToDefaults();
        LoadSettingsToUi();
        TxtSaveFeedback.Text = "Reset to default settings.";
    }

    private void OnTrySelectionClick(object sender, RoutedEventArgs e)
    {
        Hide();
        if (Application.Current is App app)
        {
            app.TriggerSelection();
        }
    }
}
