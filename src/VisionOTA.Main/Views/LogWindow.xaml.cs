using System.Windows;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    /// <summary>
    /// LogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
            var viewModel = new LogViewModel();
            DataContext = viewModel;
            Closing += (s, e) => viewModel.Dispose();
        }
    }
}
