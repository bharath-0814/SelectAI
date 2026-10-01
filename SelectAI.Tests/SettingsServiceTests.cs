using SelectAI.Settings;
using Xunit;

namespace SelectAI.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SecretEncryption_EncryptsAndDecryptsAccuratelyWithDpapi()
    {
        var settingsService = new SettingsService();
        const string testKey = "TestApiKey";
        const string secretValue = "AIzaSySecretTestToken_12345!#";

        // Save secret
        settingsService.SetSecret(testKey, secretValue);

        // Retrieve secret
        var decrypted = settingsService.GetSecret(testKey);

        Assert.Equal(secretValue, decrypted);

        // Ensure encrypted string in settings is not plaintext
        Assert.True(settingsService.CurrentSettings.EncryptedSecrets.ContainsKey(testKey));
        var cipherText = settingsService.CurrentSettings.EncryptedSecrets[testKey];
        Assert.NotEqual(secretValue, cipherText);

        // Cleanup
        settingsService.SetSecret(testKey, null);
        Assert.Null(settingsService.GetSecret(testKey));
    }

    [Fact]
    public void AppSettings_DefaultValues_AreSensible()
    {
        var settings = new AppSettings();

        Assert.Equal(Core.Enums.SelectionMode.Freeform, settings.DefaultMode);
        Assert.Equal("Control, Shift", settings.HotkeyModifiers);
        Assert.Equal("Space", settings.HotkeyKey);
        Assert.True(settings.AutoShowToolbar);
        Assert.True(settings.EnableGlowAnimation);
    }
}
