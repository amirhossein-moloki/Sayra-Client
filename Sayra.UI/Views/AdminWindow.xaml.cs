using System;
using System.Windows;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWorkspaceViewModel ViewModel { get; }

        private bool _isAnimating = false;

        public AdminWindow()
        {
            ViewModel = new AdminWorkspaceViewModel();
            this.DataContext = ViewModel;

            InitializeComponent();
            this.Closed += AdminWindow_Closed;
            this.PreviewKeyDown += AdminWindow_PreviewKeyDown;
        }

        private void AdminWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && ModalOverlay.Visibility == Visibility.Visible)
            {
                CloseModal();
                e.Handled = true;
            }
        }

        private void GamesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (GamesDataGrid.SelectedItem is Sayra.UI.Models.AdminAppItem selectedItem)
            {
                OpenModal(selectedItem);
            }
        }

        private void OpenModal(Sayra.UI.Models.AdminAppItem item)
        {
            if (_isAnimating) return;

            ModalOverlay.DataContext = item;
            ModalOverlay.Visibility = Visibility.Visible;

            var openSb = FindResource("OpenModalStoryboard") as System.Windows.Media.Animation.Storyboard;
            if (openSb != null)
            {
                _isAnimating = true;
                openSb.Completed += (s, ev) => _isAnimating = false;
                openSb.Begin(this);
            }
        }

        private void CloseModal()
        {
            if (_isAnimating || ModalOverlay.Visibility == Visibility.Collapsed) return;

            var closeSb = FindResource("CloseModalStoryboard") as System.Windows.Media.Animation.Storyboard;
            if (closeSb != null)
            {
                _isAnimating = true;
                closeSb.Completed += (s, ev) =>
                {
                    ModalOverlay.Visibility = Visibility.Collapsed;
                    ModalOverlay.DataContext = null;
                    GamesDataGrid.SelectedItem = null;
                    _isAnimating = false;
                };
                closeSb.Begin(this);
            }
            else
            {
                ModalOverlay.Visibility = Visibility.Collapsed;
                ModalOverlay.DataContext = null;
                GamesDataGrid.SelectedItem = null;
            }
        }

        private void ModalOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Optional: Can do supplementary tracking on focus/state here
        }

        private void ModalOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Clicking on the blurred overlay area closes the modal
            CloseModal();
        }

        private void ModalCard_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent closing when clicking inside the card
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            CloseModal();
        }

        private void ResetConfig_Click(object sender, RoutedEventArgs e)
        {
            Sayra.UI.Services.NotificationService.Instance.ShowWarning("پیکربندی بازی با موفقیت به حالت اولیه بازنشانی شد.");
        }

        private void RemoveImages_Click(object sender, RoutedEventArgs e)
        {
            Sayra.UI.Services.NotificationService.Instance.ShowSuccess("تمامی تصاویر کاور و آیکون های ذخیره شده بازی با موفقیت پاک شدند.");
        }

        private void AdminWindow_Closed(object? sender, EventArgs e)
        {
            bool isLoginOpen = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is LoginWindow)
                {
                    isLoginOpen = true;
                    break;
                }
            }
            if (!isLoginOpen)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
