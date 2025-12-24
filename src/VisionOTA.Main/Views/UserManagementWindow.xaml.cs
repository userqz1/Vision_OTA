using System.Windows;
using VisionOTA.Main.Helpers;
using VisionOTA.Main.ViewModels;

namespace VisionOTA.Main.Views
{
    /// <summary>
    /// UserManagementWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserManagementWindow : Window
    {
        public UserManagementWindow()
        {
            InitializeComponent();
            DataContext = new UserManagementViewModel();
            Loaded += (s, e) => WindowHelper.AdaptToScreen(this, 0.7, 0.7, 600, 450);
        }
    }
}
