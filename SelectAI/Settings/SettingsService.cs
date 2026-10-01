using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using SelectAI.Core.Interfaces;

namespace SelectAI.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelectAI");
    private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "SelectAI";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SelectAI_DPAPI_Entropy_Salt_v1");

    public AppSettings CurrentSettings { get; private set; } = new();

    public SettingsService()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    CurrentSettings = loaded;
                    if (CurrentSettings.HotkeyKey == "Space" || string.IsNullOrWhiteSpace(CurrentSettings.HotkeyKey))
                    {
                        CurrentSettings.HotkeyKey = "C";
                        SaveSettings();
                    }
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        CurrentSettings = new AppSettings();
        SaveSettings();
    }

    public void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentSettings, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public void ResetToDefaults()
    {
        CurrentSettings = new AppSettings();
        SaveSettings();
    }

    public string? GetSecret(string key)
    {
        if (!CurrentSettings.EncryptedSecrets.TryGetValue(key, out var encryptedBase64) || string.IsNullOrEmpty(encryptedBase64))
        {
            return null;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to decrypt secret '{key}': {ex.Message}");
            return null;
        }
    }

    public void SetSecret(string key, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            CurrentSettings.EncryptedSecrets.Remove(key);
        }
        else
        {
            try
            {
                var secretBytes = Encoding.UTF8.GetBytes(secret);
                var encryptedBytes = ProtectedData.Protect(secretBytes, Entropy, DataProtectionScope.CurrentUser);
                CurrentSettings.EncryptedSecrets[key] = Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to encrypt secret '{key}': {ex.Message}");
            }
        }

        SaveSettings();
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    public void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" --background");
                }
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }

            CurrentSettings.StartWithWindows = enable;
            SaveSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set auto-start in registry: {ex.Message}");
        }
    }
}
