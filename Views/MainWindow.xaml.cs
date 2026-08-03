using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SocketSimulator.ViewModels;

namespace SocketSimulator.Views
{
    public partial class MainWindow : Window
    {
        private bool _userIsInteractingWithLog;
        private System.Windows.Threading.DispatcherTimer? _autoScrollTimer;
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to LogText property changes to auto-scroll
            if (DataContext is MainViewModel vm && vm.LogVM != null)
            {
                vm.LogVM.PropertyChanged += LogVM_PropertyChanged;
            }
        }

        private void LogVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogViewModel.LogText))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_userIsInteractingWithLog)
                    {
                        AutoScrollLog();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.LogVM != null)
            {
                vm.LogVM.PropertyChanged -= LogVM_PropertyChanged;
            }
            _autoScrollTimer?.Stop();
        }

        private void LogTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (LogTextBox == null) return;
            
            // Check if user is at the bottom (allow auto-scroll)
            var scrollViewer = GetScrollViewer(LogTextBox);
            if (scrollViewer != null)
            {
                // If user is within 20 pixels of bottom, resume auto-scroll
                var atBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 20;
                if (atBottom)
                {
                    _userIsInteractingWithLog = false;
                }
            }
        }

        private void LogTextBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _userIsInteractingWithLog = true;
        }

        private void LogTextBox_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Resume auto-scroll after a short delay
            _autoScrollTimer?.Stop();
            _autoScrollTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(1000)
            };
            _autoScrollTimer.Tick += (s, args) =>
            {
                _userIsInteractingWithLog = false;
                AutoScrollLog();
                _autoScrollTimer?.Stop();
            };
            _autoScrollTimer.Start();
        }

        private void AutoScrollLog()
        {
            if (LogTextBox == null) return;
            
            var scrollViewer = GetScrollViewer(LogTextBox);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToEnd();
            }
        }

        private ScrollViewer? GetScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv)
                return sv;
            
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
