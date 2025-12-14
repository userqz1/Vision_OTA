using System.Windows;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class PlcSettingsWindow : Window
    {
        public PlcSettingsWindow()
        {
            InitializeComponent();
            var viewModel = new PlcSettingsViewModel();
            viewModel.RequestClose += (s, result) =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = viewModel;
        }
    }
}
