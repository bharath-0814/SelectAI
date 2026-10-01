using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SelectAI.Core.Interfaces;
using SelectAI.Core.Models;
using SelectAI.Core.Utils;

namespace SelectAI.UI.AI;

public partial class AskAiWindow : System.Windows.Controls.UserControl
{
    private IAIProvider? _aiProvider;
    private SelectionRegion? _currentRegion;
    private string? _currentOcrText;

    public event EventHandler? CloseRequested;

    public AskAiWindow()
    {
        InitializeComponent();
    }

    public void Setup(IAIProvider provider, SelectionRegion region, string? ocrText)
    {
        _aiProvider = provider;
        _currentRegion = region;
        _currentOcrText = ocrText;

        TxtProviderName.Text = provider.DisplayName;
        TxtResponseContent.Text = "Ask a question or tap a chip above to analyze your screen selection.";
        TxtStatus.Text = "Ready";
        PnlLoading.Visibility = Visibility.Collapsed;
        ScrollResponse.Visibility = Visibility.Visible;
    }

    public async Task AskQuestionAsync(string prompt)
    {
        if (_aiProvider == null || string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PnlLoading.Visibility = Visibility.Visible;
        ScrollResponse.Visibility = Visibility.Collapsed;
        TxtStatus.Text = "Generating answer...";

        byte[]? imgBytes = null;
        if (_currentRegion?.CroppedBitmap != null)
        {
            imgBytes = ImageHelper.ToPngBytes(_currentRegion.CroppedBitmap);
        }

        var request = new AiRequest
        {
            Prompt = prompt,
            ExtractedText = _currentOcrText,
            ImageBytes = imgBytes,
            ImageMimeType = "image/png"
        };

        try
        {
            var response = await _aiProvider.AskAsync(request);

            PnlLoading.Visibility = Visibility.Collapsed;
            ScrollResponse.Visibility = Visibility.Visible;

            if (response.Success)
            {
                TxtResponseContent.Text = response.Content;
                TxtStatus.Text = $"Answered by {response.ProviderName}";
            }
            else
            {
                TxtResponseContent.Text = $"⚠️ Error:\n{response.ErrorMessage ?? "Unknown error occurred."}";
                TxtStatus.Text = "Failed to get answer";
            }
        }
        catch (Exception ex)
        {
            PnlLoading.Visibility = Visibility.Collapsed;
            ScrollResponse.Visibility = Visibility.Visible;
            TxtResponseContent.Text = $"⚠️ Exception:\n{ex.Message}";
            TxtStatus.Text = "Error";
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        var prompt = TxtPromptInput.Text.Trim();
        if (!string.IsNullOrEmpty(prompt))
        {
            await AskQuestionAsync(prompt);
        }
    }

    private async void OnPromptKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var prompt = TxtPromptInput.Text.Trim();
            if (!string.IsNullOrEmpty(prompt))
            {
                await AskQuestionAsync(prompt);
            }
        }
    }

    private async void OnQuickChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is string text)
        {
            string cleanPrompt = text switch
            {
                var s when s.Contains("Explain") => "Explain what is in this selected screen region in clear detail.",
                var s when s.Contains("Summarize") => "Summarize the key information and takeaways from this selection.",
                var s when s.Contains("Debug") => "Identify any errors, bugs, or improvements in this code and provide the fixed version.",
                var s when s.Contains("Translate") => "Translate the text in this selection into English.",
                _ => text
            };

            TxtPromptInput.Text = cleanPrompt;
            await AskQuestionAsync(cleanPrompt);
        }
    }

    private void OnCopyResponseClick(object sender, RoutedEventArgs e)
    {
        var text = TxtResponseContent.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            System.Windows.Clipboard.SetText(text);
            TxtStatus.Text = "Copied to clipboard!";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
