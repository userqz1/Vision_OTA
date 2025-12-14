using System.ComponentModel;
using System.Windows;
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
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _viewModel?.Cleanup();
        }
    }
}
