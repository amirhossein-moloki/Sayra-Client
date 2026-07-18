using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sayra.UI.Controls
{
    public partial class Pagination : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty CurrentPageProperty =
            DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(Pagination),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged));

        public static readonly DependencyProperty TotalItemsProperty =
            DependencyProperty.Register(nameof(TotalItems), typeof(int), typeof(Pagination),
                new PropertyMetadata(0, OnTotalItemsChanged));

        public static readonly DependencyProperty PageSizeProperty =
            DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(Pagination),
                new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPageSizeChanged));

        public static readonly DependencyProperty PageChangedCommandProperty =
            DependencyProperty.Register(nameof(PageChangedCommand), typeof(ICommand), typeof(Pagination),
                new PropertyMetadata(null));

        public static readonly DependencyProperty RefreshCommandProperty =
            DependencyProperty.Register(nameof(RefreshCommand), typeof(ICommand), typeof(Pagination),
                new PropertyMetadata(null));

        public int CurrentPage
        {
            get => (int)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        public int TotalItems
        {
            get => (int)GetValue(TotalItemsProperty);
            set => SetValue(TotalItemsProperty, value);
        }

        public int PageSize
        {
            get => (int)GetValue(PageSizeProperty);
            set => SetValue(PageSizeProperty, value);
        }

        public ICommand PageChangedCommand
        {
            get => (ICommand)GetValue(PageChangedCommandProperty);
            set => SetValue(PageChangedCommandProperty, value);
        }

        public ICommand RefreshCommand
        {
            get => (ICommand)GetValue(RefreshCommandProperty);
            set => SetValue(RefreshCommandProperty, value);
        }

        // CLR Events
        public event EventHandler<PageChangedEventArgs>? PageChanged;
        public event EventHandler<PageSizeChangedEventArgs>? PageSizeChanged;

        // Smart Pagination internal list
        public ObservableCollection<PaginationItem> PageItems { get; } = new();

        private bool _isUpdatingSizeCombo = false;
        private bool _isCompact = false;

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

        public Pagination()
        {
            InitializeComponent();
            PageButtonsItemsControl.ItemsSource = PageItems;
            Loaded += Pagination_Loaded;
        }

        private void Pagination_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSizeComboSelection();
            UpdatePagination();
        }

        private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Pagination control)
            {
                control.UpdatePagination();
                control.RaisePageChanged((int)e.NewValue);
            }
        }

        private static void OnTotalItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Pagination control)
            {
                control.UpdatePagination();
            }
        }

        private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Pagination control)
            {
                control.UpdateSizeComboSelection();
                control.UpdatePagination();
                control.RaisePageSizeChanged((int)e.NewValue);
            }
        }

        private void RaisePageChanged(int page)
        {
            PageChanged?.Invoke(this, new PageChangedEventArgs(page));
            if (PageChangedCommand != null && PageChangedCommand.CanExecute(page))
            {
                PageChangedCommand.Execute(page);
            }
        }

        private void RaisePageSizeChanged(int pageSize)
        {
            PageSizeChanged?.Invoke(this, new PageSizeChangedEventArgs(pageSize));
            if (PageChangedCommand != null && PageChangedCommand.CanExecute(null))
            {
                PageChangedCommand.Execute(null);
            }
        }

        private void UpdateSizeComboSelection()
        {
            if (PageSizeComboBox == null) return;

            _isUpdatingSizeCombo = true;
            try
            {
                string sizeStr = PageSize.ToString();
                foreach (ComboBoxItem item in PageSizeComboBox.Items)
                {
                    if (item.Content?.ToString() == sizeStr)
                    {
                        PageSizeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                _isUpdatingSizeCombo = false;
            }
        }

        private void UpdatePagination()
        {
            if (PrevButton == null || NextButton == null || InfoTextBlock == null) return;

            int totalPages = TotalPages;
            int currentPage = CurrentPage;

            // Normalize currentPage
            if (totalPages > 0 && currentPage > totalPages)
            {
                CurrentPage = totalPages;
                return;
            }
            if (currentPage < 1)
            {
                CurrentPage = 1;
                return;
            }

            // Update Info Text
            int startIdx = TotalItems == 0 ? 0 : (currentPage - 1) * PageSize + 1;
            int endIdx = Math.Min(currentPage * PageSize, TotalItems);
            InfoTextBlock.Text = $"Showing {startIdx}-{endIdx} of {TotalItems} records (Total: {totalPages} Pages)";

            // Update disabled state for prev/next
            PrevButton.IsEnabled = currentPage > 1;
            NextButton.IsEnabled = currentPage < totalPages;

            // Generate dynamic items
            PageItems.Clear();

            if (totalPages <= 1)
            {
                PageItems.Add(new PaginationItem { Text = "1", PageNumber = 1, IsSelected = true });
                return;
            }

            if (_isCompact)
            {
                // Compact mode: only show current page
                PageItems.Add(new PaginationItem { Text = currentPage.ToString(), PageNumber = currentPage, IsSelected = true });
                return;
            }

            // Always add first page
            PageItems.Add(new PaginationItem { Text = "1", PageNumber = 1, IsSelected = currentPage == 1 });

            // Smart pagination range
            int middleCount = 3;
            int half = middleCount / 2;
            int start = currentPage - half;
            int end = currentPage + half;

            if (start <= 2)
            {
                start = 2;
                end = Math.Min(totalPages - 1, start + middleCount - 1);
            }
            else if (end >= totalPages - 1)
            {
                end = totalPages - 1;
                start = Math.Max(2, end - middleCount + 1);
            }

            // First ellipsis
            if (start > 2)
            {
                PageItems.Add(new PaginationItem { Text = "...", IsEllipsis = true });
            }

            // Middle pages
            for (int i = start; i <= end; i++)
            {
                PageItems.Add(new PaginationItem { Text = i.ToString(), PageNumber = i, IsSelected = currentPage == i });
            }

            // Second ellipsis
            if (end < totalPages - 1)
            {
                PageItems.Add(new PaginationItem { Text = "...", IsEllipsis = true });
            }

            // Always add last page
            PageItems.Add(new PaginationItem { Text = totalPages.ToString(), PageNumber = totalPages, IsSelected = currentPage == totalPages });
        }

        private void Pagination_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Responsiveness: trigger compact layout if width is less than 650
            bool shouldBeCompact = e.NewSize.Width < 650;
            if (shouldBeCompact != _isCompact)
            {
                _isCompact = shouldBeCompact;
                if (_isCompact)
                {
                    RowsPerPagePanel.Visibility = Visibility.Collapsed;
                    JumpToPagePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RowsPerPagePanel.Visibility = Visibility.Visible;
                    JumpToPagePanel.Visibility = Visibility.Visible;
                }
                UpdatePagination();
            }
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSizeCombo || PageSizeComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            if (int.TryParse(selectedItem.Content?.ToString(), out int newSize))
            {
                PageSize = newSize;
                CurrentPage = 1; // Reset to page 1
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
            }
        }

        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PaginationItem item && !item.IsEllipsis)
            {
                CurrentPage = item.PageNumber;
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteJump();
        }

        private void JumpPageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteJump();
            }
        }

        private void ExecuteJump()
        {
            if (string.IsNullOrWhiteSpace(JumpPageTextBox.Text)) return;

            if (int.TryParse(JumpPageTextBox.Text, out int targetPage))
            {
                if (targetPage >= 1 && targetPage <= TotalPages)
                {
                    CurrentPage = targetPage;
                    JumpPageTextBox.Text = string.Empty;
                }
                else
                {
                    MessageBox.Show($"Please enter a page number between 1 and {TotalPages}.", "Invalid Page", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (RefreshCommand != null && RefreshCommand.CanExecute(null))
            {
                RefreshCommand.Execute(null);
            }
        }
    }

    public class PaginationItem
    {
        public string Text { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public bool IsSelected { get; set; }
        public bool IsEllipsis { get; set; }

        public Visibility IsEllipsisVisibility => IsEllipsis ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsButtonVisibility => IsEllipsis ? Visibility.Collapsed : Visibility.Visible;
    }

    public class PageChangedEventArgs : EventArgs
    {
        public int CurrentPage { get; }
        public PageChangedEventArgs(int currentPage) => CurrentPage = currentPage;
    }

    public class PageSizeChangedEventArgs : EventArgs
    {
        public int PageSize { get; }
        public PageSizeChangedEventArgs(int pageSize) => PageSize = pageSize;
    }
}
