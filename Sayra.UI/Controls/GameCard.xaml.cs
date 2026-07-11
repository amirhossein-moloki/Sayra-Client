using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

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
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(GameCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(GameCard),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty IsAvailableProperty =
            DependencyProperty.Register(nameof(IsAvailable), typeof(bool), typeof(GameCard),
                new PropertyMetadata(true, OnStateChanged));

        public static readonly DependencyProperty PlayCommandProperty =
            DependencyProperty.Register(nameof(PlayCommand), typeof(ICommand), typeof(GameCard),
                new PropertyMetadata(null));

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

        private bool _isMouseOver;

        public GameCard()
        {
            InitializeComponent();
            Loaded += (s, e) => AnimateToState(immediate: true);
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card)
            {
                card.AnimateToState();
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

        private void AnimateToState(bool immediate = false)
        {
            double durationSec = immediate ? 0.0 : 0.25;
            var duration = TimeSpan.FromSeconds(durationSec);

            double targetScale = 1.0;
            Color targetBorderColor = Color.FromArgb(30, 255, 255, 255); // #1FFFFFFF (rgba(255,255,255,0.12))
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
                targetBorderColor = Color.FromArgb(20, 255, 255, 255);
                targetShadowOpacity = 0.2;
                targetShadowBlur = 10;
                targetShadowDepth = 2;
                targetDisabledOverlayOpacity = 0.5;
            }
            else if (IsSelected)
            {
                // Selected State
                targetBorderColor = Color.FromRgb(245, 255, 0); // Yellow
                targetShadowColor = Color.FromRgb(245, 255, 0); // Yellow glow
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
                targetBorderColor = Color.FromArgb(160, 245, 255, 0); // Bright semi-trans yellow
                targetShadowColor = Color.FromRgb(245, 255, 0); // Yellow glow
                targetShadowOpacity = 0.55;
                targetShadowBlur = 20;
                targetShadowDepth = 0;
            }

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
