using System.Windows;
using System.Windows.Controls;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            Closing += OnClosing;

            // 处理密码框变更
            PasswordBox.PasswordChanged += OnPasswordChanged;

            // 登录面板关闭或密码清空时清空密码框
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsLoginPanelVisible) && !_viewModel.IsLoginPanelVisible)
                {
                    PasswordBox.Clear();
                }
                else if (args.PropertyName == nameof(MainViewModel.LoginPassword) && string.IsNullOrEmpty(_viewModel.LoginPassword))
                {
                    PasswordBox.Clear();
                }
            };
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                _viewModel.LoginPassword = pb.Password;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _viewModel.Cleanup();
            }
            catch { }

            // 确保应用程序完全退出
            Application.Current.Shutdown();
        }
    }
}
