using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VisionOTA.Common.Mvvm;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Permission;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 用户管理视图模型
    /// </summary>
    public class UserManagementViewModel : ViewModelBase
    {
        private UserDisplayModel _selectedUser;
        private string _statusMessage;

        public ObservableCollection<UserDisplayModel> Users { get; } = new ObservableCollection<UserDisplayModel>();

        public UserDisplayModel SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand AddUserCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand DeleteUserCommand { get; }

        public UserManagementViewModel()
        {
            Title = "用户管理";

            AddUserCommand = new RelayCommand(_ => AddUser());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword(), _ => SelectedUser != null);
            DeleteUserCommand = new RelayCommand(_ => DeleteUser(), _ => SelectedUser != null && !SelectedUser.IsDefaultAdmin);

            LoadUsers();
        }

        private void LoadUsers()
        {
            Users.Clear();
            var users = PermissionService.Instance.GetAllUsers();
            foreach (var user in users)
            {
                Users.Add(new UserDisplayModel(user));
            }
            StatusMessage = $"共 {Users.Count} 个用户";
        }

        private void AddUser()
        {
            var dialog = new AddUserDialog();
            if (dialog.ShowDialog() == true)
            {
                var username = dialog.Username;
                var password = dialog.Password;
                var level = dialog.SelectedLevel;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("用户名和密码不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (PermissionService.Instance.AddUser(username, password, level))
                {
                    LoadUsers();
                    StatusMessage = $"用户 {username} 添加成功";
                }
                else
                {
                    MessageBox.Show("添加用户失败，用户名可能已存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ChangePassword()
        {
            if (SelectedUser == null)
                return;

            var dialog = new ChangePasswordDialog(SelectedUser.Username);
            if (dialog.ShowDialog() == true)
            {
                var newPassword = dialog.NewPassword;
                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    MessageBox.Show("密码不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (PermissionService.Instance.ChangePassword(SelectedUser.Username, newPassword))
                {
                    StatusMessage = $"用户 {SelectedUser.Username} 密码修改成功";
                    MessageBox.Show("密码修改成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("密码修改失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteUser()
        {
            if (SelectedUser == null || SelectedUser.IsDefaultAdmin)
                return;

            var result = MessageBox.Show(
                $"确定要删除用户 {SelectedUser.Username} 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (PermissionService.Instance.DeleteUser(SelectedUser.Username))
                {
                    LoadUsers();
                    StatusMessage = $"用户 {SelectedUser.Username} 已删除";
                }
                else
                {
                    MessageBox.Show("删除用户失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// 用户显示模型
    /// </summary>
    public class UserDisplayModel
    {
        private readonly User _user;

        public UserDisplayModel(User user)
        {
            _user = user;
        }

        public string Username => _user.Username;
        public bool IsDefaultAdmin => _user.IsDefaultAdmin;
        public DateTime CreatedTime => _user.CreatedTime;
        public DateTime? LastLoginTime => _user.LastLoginTime;

        public string LevelDisplay
        {
            get
            {
                switch (_user.Level)
                {
                    case PermissionLevel.Operator: return "操作员";
                    case PermissionLevel.Engineer: return "工程师";
                    case PermissionLevel.Administrator: return "管理员";
                    default: return "无";
                }
            }
        }

        public string StatusDisplay => _user.IsEnabled ? "启用" : "禁用";
        public string Remark => _user.IsDefaultAdmin ? "系统默认管理员" : "";
    }

    /// <summary>
    /// 添加用户对话框
    /// </summary>
    public class AddUserDialog : Window
    {
        private System.Windows.Controls.TextBox _usernameBox;
        private System.Windows.Controls.PasswordBox _passwordBox;
        private System.Windows.Controls.ComboBox _levelBox;

        public string Username => _usernameBox.Text;
        public string Password => _passwordBox.Password;
        public PermissionLevel SelectedLevel
        {
            get
            {
                switch (_levelBox.SelectedIndex)
                {
                    case 0: return PermissionLevel.Operator;
                    case 1: return PermissionLevel.Engineer;
                    case 2: return PermissionLevel.Administrator;
                    default: return PermissionLevel.Operator;
                }
            }
        }

        public AddUserDialog()
        {
            Title = "添加用户";
            Width = 350;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F3F0"));

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblUsername = new System.Windows.Controls.TextBlock { Text = "用户名:", VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(lblUsername, 0);
            System.Windows.Controls.Grid.SetColumn(lblUsername, 0);
            grid.Children.Add(lblUsername);

            _usernameBox = new System.Windows.Controls.TextBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(_usernameBox, 0);
            System.Windows.Controls.Grid.SetColumn(_usernameBox, 1);
            grid.Children.Add(_usernameBox);

            var lblPassword = new System.Windows.Controls.TextBlock { Text = "密码:", VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(lblPassword, 1);
            System.Windows.Controls.Grid.SetColumn(lblPassword, 0);
            grid.Children.Add(lblPassword);

            _passwordBox = new System.Windows.Controls.PasswordBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(_passwordBox, 1);
            System.Windows.Controls.Grid.SetColumn(_passwordBox, 1);
            grid.Children.Add(_passwordBox);

            var lblLevel = new System.Windows.Controls.TextBlock { Text = "权限:", VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(lblLevel, 2);
            System.Windows.Controls.Grid.SetColumn(lblLevel, 0);
            grid.Children.Add(lblLevel);

            _levelBox = new System.Windows.Controls.ComboBox { Height = 32 };
            _levelBox.Items.Add("操作员");
            _levelBox.Items.Add("工程师");
            _levelBox.Items.Add("管理员");
            _levelBox.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(_levelBox, 2);
            System.Windows.Controls.Grid.SetColumn(_levelBox, 1);
            grid.Children.Add(_levelBox);

            var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            System.Windows.Controls.Grid.SetRow(btnPanel, 4);
            System.Windows.Controls.Grid.SetColumnSpan(btnPanel, 2);

            var btnOk = new System.Windows.Controls.Button { Content = "确定", Width = 80, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += (s, e) => { DialogResult = true; };
            btnPanel.Children.Add(btnOk);

            var btnCancel = new System.Windows.Controls.Button { Content = "取消", Width = 80, Height = 32 };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            btnPanel.Children.Add(btnCancel);

            grid.Children.Add(btnPanel);
            Content = grid;
        }
    }

    /// <summary>
    /// 修改密码对话框
    /// </summary>
    public class ChangePasswordDialog : Window
    {
        private System.Windows.Controls.PasswordBox _newPasswordBox;
        private System.Windows.Controls.PasswordBox _confirmPasswordBox;

        public string NewPassword => _newPasswordBox.Password;

        public ChangePasswordDialog(string username)
        {
            Title = $"修改密码 - {username}";
            Width = 350;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F3F0"));

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(40) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblNew = new System.Windows.Controls.TextBlock { Text = "新密码:", VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(lblNew, 0);
            System.Windows.Controls.Grid.SetColumn(lblNew, 0);
            grid.Children.Add(lblNew);

            _newPasswordBox = new System.Windows.Controls.PasswordBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(_newPasswordBox, 0);
            System.Windows.Controls.Grid.SetColumn(_newPasswordBox, 1);
            grid.Children.Add(_newPasswordBox);

            var lblConfirm = new System.Windows.Controls.TextBlock { Text = "确认密码:", VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(lblConfirm, 1);
            System.Windows.Controls.Grid.SetColumn(lblConfirm, 0);
            grid.Children.Add(lblConfirm);

            _confirmPasswordBox = new System.Windows.Controls.PasswordBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetRow(_confirmPasswordBox, 1);
            System.Windows.Controls.Grid.SetColumn(_confirmPasswordBox, 1);
            grid.Children.Add(_confirmPasswordBox);

            var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            System.Windows.Controls.Grid.SetRow(btnPanel, 3);
            System.Windows.Controls.Grid.SetColumnSpan(btnPanel, 2);

            var btnOk = new System.Windows.Controls.Button { Content = "确定", Width = 80, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += (s, e) =>
            {
                if (_newPasswordBox.Password != _confirmPasswordBox.Password)
                {
                    MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
            };
            btnPanel.Children.Add(btnOk);

            var btnCancel = new System.Windows.Controls.Button { Content = "取消", Width = 80, Height = 32 };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            btnPanel.Children.Add(btnCancel);

            grid.Children.Add(btnPanel);
            Content = grid;
        }
    }
}
