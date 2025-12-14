using System.Windows;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            DataContext = _viewModel;
        }

        private void OnLoginSucceeded(object sender, System.EventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}
