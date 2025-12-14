using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionOTA.Common.Mvvm;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Permission;

namespace VisionOTA.Main.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username = "admin";
        private string _errorMessage;
        private bool _rememberUsername;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool RememberUsername
        {
            get => _rememberUsername;
            set => SetProperty(ref _rememberUsername, value);
        }

        public ICommand LoginCommand { get; }

        public event EventHandler LoginSucceeded;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        private void ExecuteLogin(object parameter)
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "请输入用户名";
                return;
            }

            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? string.Empty;

            if (string.IsNullOrEmpty(password))
            {
                ErrorMessage = "请输入密码";
                return;
            }

            var success = PermissionService.Instance.Login(Username, password);

            if (success)
            {
                FileLogger.Instance.Info($"用户 {Username} 登录成功", "Login");
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = "用户名或密码错误";
                FileLogger.Instance.Warning($"用户 {Username} 登录失败", "Login");
            }
        }
    }
}
