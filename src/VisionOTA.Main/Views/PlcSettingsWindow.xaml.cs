using System.ComponentModel;
using System.Windows;
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

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // 确保PLC连接被正确断开
            _viewModel?.Cleanup();
        }
    }
}
