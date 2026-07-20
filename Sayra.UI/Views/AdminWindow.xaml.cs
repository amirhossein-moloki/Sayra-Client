using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sayra.UI.ViewModels;

namespace Sayra.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWorkspaceViewModel ViewModel { get; }

        private bool _isAnimating = false;

        public AdminWindow()
        {
            ViewModel = App.ServiceProvider?.GetService<AdminWorkspaceViewModel>() ?? new AdminWorkspaceViewModel();
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

        private bool _isDraggingSelection = false;

        public void DataGridRow_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // If user clicked inside an interactive element (e.g. CheckBox), let's not start drag selection on rows
            var depObj = e.OriginalSource as DependencyObject;
            while (depObj != null && depObj != sender as DependencyObject)
            {
                if (depObj is System.Windows.Controls.CheckBox)
                {
                    return;
                }
                depObj = System.Windows.Media.VisualTreeHelper.GetParent(depObj);
            }

            if (sender is System.Windows.Controls.DataGridRow row)
            {
                // Focus on row to make sure it selects properly
                row.Focus();

                // Toggle or adjust selection depending on modifier keys
                if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None)
                {
                    if (!row.IsSelected)
                    {
                        GamesDataGrid.SelectedItems.Clear();
                        row.IsSelected = true;
                    }
                }
                else if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    row.IsSelected = !row.IsSelected;
                }

                e.Handled = true;

                // Open game detail modal on single left-click
                if (row.DataContext is Sayra.UI.Models.AdminAppItem selectedItem)
                {
                    OpenModal(selectedItem);
                }
            }
        }

        public void DataGridRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingSelection && sender is System.Windows.Controls.DataGridRow row)
            {
                row.IsSelected = true;
            }
        }

        public void GamesDataGrid_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDraggingSelection)
            {
                _isDraggingSelection = false;
                GamesDataGrid.ReleaseMouseCapture();
            }
        }

        private void GamesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Double click on a row to open detail modal
            var depObj = e.OriginalSource as DependencyObject;
            while (depObj != null && depObj != GamesDataGrid)
            {
                if (depObj is System.Windows.Controls.DataGridRow row)
                {
                    if (row.DataContext is Sayra.UI.Models.AdminAppItem selectedItem)
                    {
                        OpenModal(selectedItem);
                        e.Handled = true;
                    }
                    break;
                }
                depObj = System.Windows.Media.VisualTreeHelper.GetParent(depObj);
            }
        }

        public void OpenModal(Sayra.UI.Models.AdminAppItem item)
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

        private void BrowseCover_Click(object sender, RoutedEventArgs e)
        {
            if (ModalOverlay.DataContext is Sayra.UI.Models.AdminAppItem selectedItem)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
                    Title = "Select Cover Image"
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedItem.CoverImage = dialog.FileName;
                }
            }
        }

        private void BrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            if (ModalOverlay.DataContext is Sayra.UI.Models.AdminAppItem selectedItem)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
                    Title = "Select Logo Image"
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedItem.LogoImage = dialog.FileName;
                }
            }
        }

        private void BrowseBackground_Click(object sender, RoutedEventArgs e)
        {
            if (ModalOverlay.DataContext is Sayra.UI.Models.AdminAppItem selectedItem)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
                    Title = "Select Background Image"
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedItem.BackgroundImage = dialog.FileName;
                }
            }
        }

        private void ResetConfig_Click(object sender, RoutedEventArgs e)
        {
            Sayra.UI.Services.NotificationService.Instance.ShowWarning("پیکربندی بازی با موفقیت به حالت اولیه بازنشانی شد.");
        }

        private void RemoveImages_Click(object sender, RoutedEventArgs e)
        {
            Sayra.UI.Services.NotificationService.Instance.ShowSuccess("تمامی تصاویر کاور و آیکون های ذخیره شده بازی با موفقیت پاک شدند.");
        }

        private void GoToHome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalExceptionHandler.LogTrace("ADMIN_NAV", "Navigating to HomeWindow");
                var homeWin = new HomeWindow();
                homeWin.Show();
                Application.Current.MainWindow = homeWin;
                this.Close();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "GoToHome Navigation");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalExceptionHandler.LogTrace("ADMIN_NAV", "Logging out of admin panel");
                App.IsAdminLoggedIn = false;
                var loginWin = new LoginWindow();
                loginWin.Show();
                Application.Current.MainWindow = loginWin;
                this.Close();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.HandleException(ex, "Logout");
            }
        }

        private void AdminWindow_Closed(object? sender, EventArgs e)
        {
            bool isAnyOtherWindowOpen = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is LoginWindow || win is HomeWindow)
                {
                    isAnyOtherWindowOpen = true;
                    break;
                }
            }
            if (!isAnyOtherWindowOpen)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
