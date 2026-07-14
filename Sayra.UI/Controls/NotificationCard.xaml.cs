using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Sayra.UI.Controls;

public partial class NotificationCard : UserControl
{
    private Storyboard? _showStoryboard;
    private Storyboard? _hideStoryboard;
    private Storyboard? _loadingPulseStoryboard;

    public NotificationCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _showStoryboard = Resources["ShowStoryboard"] as Storyboard;
        _hideStoryboard = Resources["HideStoryboard"] as Storyboard;
        _loadingPulseStoryboard = Resources["LoadingPulseStoryboard"] as Storyboard;

        Sayra.UI.Services.NotificationService.Instance.CloseRequested += OnCloseRequested;

        TriggerEntranceAnimation();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            TriggerEntranceAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Sayra.UI.Services.NotificationService.Instance.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(Action onCompleted)
    {
        TriggerExitAnimation(onCompleted);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateIconSource();
    }

    public void TriggerEntranceAnimation()
    {
        _showStoryboard?.Begin(this);

        // Check if loading pulse is needed
        dynamic? context = DataContext;
        if (context != null)
        {
            try
            {
                string type = context.NotificationType?.ToString() ?? "";
                if (type == "Loading")
                {
                    _loadingPulseStoryboard?.Begin(this, true);
                }
                else
                {
                    _loadingPulseStoryboard?.Stop(this);
                }
            }
            catch
            {
                // dynamic binding safety fallback
            }
        }
    }

    public void TriggerExitAnimation(Action onCompleted)
    {
        if (_hideStoryboard != null)
        {
            _hideStoryboard.Completed += (s, e) => onCompleted();
            _hideStoryboard.Begin(this);
        }
        else
        {
            onCompleted();
        }
    }

    private void UpdateIconSource()
    {
        dynamic? context = DataContext;
        if (context == null)
        {
            IconViewbox.Source = null!;
            return;
        }

        try
        {
            string type = context.NotificationType?.ToString() ?? "";
            string iconName = type.ToLower() switch
            {
                "error" => "failed.svg",
                "success" => "success.svg",
                "warning" => "info.svg",
                "loading" => "loading.svg",
                _ => "info.svg"
            };

            // Shared pack URI that accesses the linked assets properly.
            // Absolute uri with application component prefix resolves properly in WPF.
            string uriString = $"/Sayra.UI;component/Assets/{iconName}";

            // Check if standard resource is loaded or fall back gracefully
            IconViewbox.Source = new Uri(uriString, UriKind.RelativeOrAbsolute);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCard] Error resolving icon source: {ex.Message}");
            IconViewbox.Source = null!;
        }
    }
}
