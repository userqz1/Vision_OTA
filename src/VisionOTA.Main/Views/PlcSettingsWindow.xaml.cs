using System.ComponentModel;
using System.Windows;
using VisionOTA.Main.Helpers;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class PlcSettingsWindow : Window
    {
        private PlcSettingsViewModel _viewModel;

        public PlcSettingsWindow()
        {
            InitializeComponent();
            _viewModel = new PlcSettingsViewModel();
            _viewModel.RequestClose += (s, result) =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = _viewModel;

            // 窗口关闭时清理资源
            Closing += OnWindowClosing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 自适应屏幕尺寸
            WindowHelper.AdaptToScreen(this, 0.8, 0.8, 650, 400);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // 确保PLC连接被正确断开
            _viewModel?.Cleanup();
        }
    }
}
