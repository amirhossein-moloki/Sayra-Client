using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Sayra.UI.Controls
{
    public partial class GameHero : UserControl
    {
        public Image CoverImage => DetailCoverImage;
        public Image LogoImage => DetailLogoImage;
        public TextBlock TitleText => DetailTitle;
        public TextBlock GenreText => DetailGenre;
        public TextBlock DescriptionText => DetailDescription;
        public GameBadge DeveloperBadgeControl => DeveloperBadge;
        public GameBadge ReleaseYearBadgeControl => ReleaseYearBadge;
        public GameBadge LauncherBadgeControl => LauncherBadge;
        public Button PlayButtonControl => PlayButton;
        public Border StatusBadgeBorderControl => StatusBadgeBorder;
        public Ellipse StatusBadgeDotControl => StatusBadgeDot;
        public TextBlock StatusText => DetailStatus;

        public GameHero()
        {
            InitializeComponent();
        }

        private void DetailCoverImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try
            {
                if (sender is Image img)
                {
                    img.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Suppress
            }
        }
    }
}
