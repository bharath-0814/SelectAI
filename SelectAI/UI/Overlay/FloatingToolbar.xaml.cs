using System.Windows;
using System.Windows.Controls;
using SelectAI.Core.Models;

namespace SelectAI.UI.Overlay;

public partial class FloatingToolbar : System.Windows.Controls.UserControl
{
    public event EventHandler? AskAiRequested;
    public event EventHandler? GoogleSearchRequested;
    public event EventHandler? GoogleLensRequested;
    public event EventHandler? ExtractTextRequested;
    public event EventHandler? TranslateRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<DetectedEntity>? SmartActionRequested;

    private DetectedEntity? _currentEntity;

    public FloatingToolbar()
    {
        InitializeComponent();
    }

    public void SetDetectedEntity(DetectedEntity? entity)
    {
        _currentEntity = entity;
        if (entity != null)
        {
            SmartActionIcon.Text = entity.ActionIcon;
            SmartActionText.Text = entity.DisplayLabel;
            SmartActionButton.Content = entity.Type switch
            {
                Core.Enums.ContentType.Url => "Open",
                Core.Enums.ContentType.Email => "Compose",
                Core.Enums.ContentType.Code => "Explain",
                Core.Enums.ContentType.SearchQuery => "Search",
                _ => "Go"
            };
            SmartActionBanner.Visibility = Visibility.Visible;
        }
        else
        {
            SmartActionBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAskAiClick(object sender, RoutedEventArgs e) => AskAiRequested?.Invoke(this, EventArgs.Empty);
    private void OnGoogleSearchClick(object sender, RoutedEventArgs e) => GoogleSearchRequested?.Invoke(this, EventArgs.Empty);
    private void OnGoogleLensClick(object sender, RoutedEventArgs e) => GoogleLensRequested?.Invoke(this, EventArgs.Empty);
    private void OnExtractTextClick(object sender, RoutedEventArgs e) => ExtractTextRequested?.Invoke(this, EventArgs.Empty);
    private void OnTranslateClick(object sender, RoutedEventArgs e) => TranslateRequested?.Invoke(this, EventArgs.Empty);
    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, EventArgs.Empty);
    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnSmartActionClick(object sender, RoutedEventArgs e)
    {
        if (_currentEntity != null)
        {
            SmartActionRequested?.Invoke(this, _currentEntity);
        }
    }
}
