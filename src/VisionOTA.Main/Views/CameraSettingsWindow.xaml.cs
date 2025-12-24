using System.ComponentModel;
using System.Windows;
using VisionOTA.Main.Helpers;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class CameraSettingsWindow : Window
    {
        private readonly CameraSettingsViewModel _viewModel;

        public CameraSettingsWindow()
        {
            InitializeComponent();
            _viewModel = new CameraSettingsViewModel();
            _viewModel.RequestClose += (s, result) =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = _viewModel;
            Closing += OnWindowClosing;
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // 自适应屏幕尺寸
            WindowHelper.AdaptToScreen(this, 0.9, 0.9, 900, 650);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _viewModel?.Cleanup();
        }
    }
}
