using System.Windows;
using System.Windows.Controls;

namespace SelectAI.UI.Overlay;

public partial class FloatingToolbar : UserControl
{
    public event EventHandler? CopyRequested;
    public event EventHandler? ShareRequested;
    public event EventHandler? SaveRequested;

    public FloatingToolbar()
    {
        InitializeComponent();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, EventArgs.Empty);
    private void OnShareClick(object sender, RoutedEventArgs e) => ShareRequested?.Invoke(this, EventArgs.Empty);
    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
}
