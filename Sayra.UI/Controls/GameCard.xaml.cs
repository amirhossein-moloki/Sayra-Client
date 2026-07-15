using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Sayra.UI.Controls
{
    public partial class GameCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(GameCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty GenreProperty =
            DependencyProperty.Register(nameof(Genre), typeof(string), typeof(GameCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ImagePathProperty =
            DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(GameCard),
                new PropertyMetadata(string.Empty, OnImagePathChanged));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(GameCard),
                new PropertyMetadata(string.Empty, OnStateChanged));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(GameCard),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty IsAvailableProperty =
            DependencyProperty.Register(nameof(IsAvailable), typeof(bool), typeof(GameCard),
                new PropertyMetadata(true, OnStateChanged));

        public static readonly DependencyProperty PlayCommandProperty =
            DependencyProperty.Register(nameof(PlayCommand), typeof(ICommand), typeof(GameCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(GameCard),
                new PropertyMetadata("PLAY"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Genre
        {
            get => (string)GetValue(GenreProperty);
            set => SetValue(GenreProperty, value);
        }

        public string ImagePath
        {
            get => (string)GetValue(ImagePathProperty);
            set => SetValue(ImagePathProperty, value);
        }

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsAvailable
        {
            get => (bool)GetValue(IsAvailableProperty);
            set => SetValue(IsAvailableProperty, value);
        }

        public ICommand PlayCommand
        {
            get => (ICommand)GetValue(PlayCommandProperty);
            set => SetValue(PlayCommandProperty, value);
        }

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        private bool _isMouseOver;

        public GameCard()
        {
            InitializeComponent();
            Loaded += (s, e) => {
                UpdateCoverImage();
                AnimateToState(immediate: true);
            };
        }

        private void CoverImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try
            {
                if (sender is Image img)
                {
                    img.Visibility = Visibility.Collapsed;
                }
                System.Diagnostics.Debug.WriteLine($"[GameCard] Failed to load image: {e.ErrorException?.Message}");
            }
            catch
            {
                // Suppress any errors during failure handling
            }
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card)
            {
                card.AnimateToState();
            }
        }

        private static void OnImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card)
            {
                card.UpdateCoverImage();
            }
        }

        private void UpdateCoverImage()
        {
            if (CoverImage == null) return;

            string path = ImagePath;
            if (string.IsNullOrEmpty(path))
            {
                CoverImage.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                if (path.StartsWith("pack://") || path.Contains("://"))
                {
                    bitmap.UriSource = new Uri(path);
                }
                else
                {
                    string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    }
                }

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze(); // Optimize rendering memory and prevent UI freezes

                CoverImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameCard] Error loading image {path}: {ex.Message}");
                CoverImage.Source = null;
            }
        }

        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOver = true;
            AnimateToState();
        }

        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOver = false;
            AnimateToState();
        }

        private void Card_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Walk the visual tree up from original source to see if we clicked the Play button
            DependencyObject? obj = e.OriginalSource as DependencyObject;
            while (obj != null)
            {
                if (obj is Button btn)
                {
                    // Clicked on the Play Button itself, let the command execute
                    return;
                }
                if (obj == this)
                {
                    break;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }

            // Clicked outside the Play Button, navigate to Game Detail!
            try
            {
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow is Sayra.UI.Views.HomeWindow dashboard)
                {
                    // Map the DataContext which is our GameItem model
                    if (this.DataContext is Sayra.UI.Models.GameItem gameItem)
                    {
                        dashboard.OpenGameDetail(gameItem);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameCard] Navigation to detail failed: {ex.Message}");
            }
        }

        private void AnimateToState(bool immediate = false)
        {
            double durationSec = immediate ? 0.0 : 0.25;
            var duration = TimeSpan.FromSeconds(durationSec);

            double targetScale = 1.0;
            Color targetBorderColor = Color.FromRgb(37, 37, 40); // #252528 (Border Color)
            Color targetShadowColor = Color.FromRgb(0, 0, 0);
            double targetShadowOpacity = 0.5;
            double targetShadowBlur = 15;
            double targetShadowDepth = 4;
            double targetIndicatorOpacity = 0.0;
            double targetDisabledOverlayOpacity = 0.0;
            double targetCardOpacity = 1.0;

            if (!IsAvailable)
            {
                // Disabled State
                targetCardOpacity = 0.55;
                targetBorderColor = Color.FromArgb(80, 37, 37, 40); // #252528 at low opacity
                targetShadowOpacity = 0.2;
                targetShadowBlur = 10;
                targetShadowDepth = 2;
                targetDisabledOverlayOpacity = 0.5;
            }
            else if (IsSelected)
            {
                // Selected State
                targetBorderColor = Color.FromRgb(255, 255, 61); // #ffff3d (Primary Yellow)
                targetShadowColor = Color.FromRgb(255, 255, 61); // #ffff3d glow
                targetShadowOpacity = _isMouseOver ? 0.8 : 0.65;
                targetShadowBlur = _isMouseOver ? 25 : 20;
                targetShadowDepth = 0;
                targetScale = _isMouseOver ? 1.03 : 1.0;
                targetIndicatorOpacity = 1.0;
            }
            else if (_isMouseOver)
            {
                // Hover State
                targetScale = 1.03;
                targetBorderColor = Color.FromArgb(160, 244, 244, 107); // #f4f46b (Primary Hover Yellow)
                targetShadowColor = Color.FromRgb(255, 255, 61); // #ffff3d glow
                targetShadowOpacity = 0.55;
                targetShadowBlur = 20;
                targetShadowDepth = 0;
            }

            // Update dynamic Button Text based on Status and availability
            string targetButtonText = "PLAY";
            if (!string.IsNullOrEmpty(Status))
            {
                string upperStatus = Status.ToUpperInvariant();
                if (upperStatus == "CURRENTLY PLAYING" || upperStatus == "PLAYING" || upperStatus == "RUNNING")
                {
                    targetButtonText = "PLAYING";
                }
                else if (upperStatus == "LOCKED")
                {
                    targetButtonText = "LOCKED";
                }
                else if (upperStatus == "UNAVAILABLE")
                {
                    targetButtonText = "UNAVAILABLE";
                }
                else if (upperStatus == "INSTALLED")
                {
                    targetButtonText = "PLAY";
                }
            }
            else if (!IsAvailable)
            {
                targetButtonText = "UNAVAILABLE";
            }
            ButtonText = targetButtonText;

            // Animate Scale
            if (CardScaleTransform != null)
            {
                CardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration), HandoffBehavior.SnapshotAndReplace);
                CardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration), HandoffBehavior.SnapshotAndReplace);
            }

            // Animate Border Brush
            if (CardBorderBrushInstance != null)
            {
                CardBorderBrushInstance.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(targetBorderColor, duration), HandoffBehavior.SnapshotAndReplace);
            }

            // Animate Shadow properties
            if (CardShadowInstance != null)
            {
                CardShadowInstance.BeginAnimation(DropShadowEffect.ColorProperty, new ColorAnimation(targetShadowColor, duration), HandoffBehavior.SnapshotAndReplace);
                CardShadowInstance.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(targetShadowOpacity, duration), HandoffBehavior.SnapshotAndReplace);
                CardShadowInstance.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(targetShadowBlur, duration), HandoffBehavior.SnapshotAndReplace);
                CardShadowInstance.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(targetShadowDepth, duration), HandoffBehavior.SnapshotAndReplace);
            }

            // Animate Overlays & Indicators
            if (SelectedIndicator != null)
            {
                SelectedIndicator.BeginAnimation(OpacityProperty, new DoubleAnimation(targetIndicatorOpacity, duration), HandoffBehavior.SnapshotAndReplace);
            }
            if (DisabledOverlay != null)
            {
                DisabledOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(targetDisabledOverlayOpacity, duration), HandoffBehavior.SnapshotAndReplace);
            }
            if (CardBorder != null)
            {
                CardBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(targetCardOpacity, duration), HandoffBehavior.SnapshotAndReplace);
            }
        }
    }
}
