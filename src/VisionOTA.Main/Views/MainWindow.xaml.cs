using System.Threading;
using System.Windows;
using System.Windows.Controls;
using VisionOTA.Hardware.Camera;
using VisionOTA.Infrastructure.Logging;
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
            FileLogger.Instance.Info("MainWindow 开始关闭...", "App");

            try
            {
                // 先释放 ViewModel（会停止检测服务和相机采集）
                _viewModel.Cleanup();
            }
            catch { }

            // 等待一下让清理完成
            Thread.Sleep(200);

            try
            {
                // 直接释放相机资源（确保相机被关闭）
                CameraManager.Instance.Dispose();
                FileLogger.Instance.Info("相机资源已在窗口关闭时释放", "App");
            }
            catch { }

            // 等待相机完全关闭
            Thread.Sleep(300);

            // 确保应用程序完全退出
            Application.Current.Shutdown();
        }
    }
}
