using System.Windows;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class VisionSettingsWindow : Window
    {
        public VisionSettingsWindow()
        {
            InitializeComponent();
            var viewModel = new VisionSettingsViewModel();
            viewModel.RequestClose += (s, result) =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = viewModel;
        }
    }
}
