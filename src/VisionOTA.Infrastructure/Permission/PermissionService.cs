using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VisionOTA.Common.Events;
using VisionOTA.Common.Extensions;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Infrastructure.Permission
{
    /// <summary>
    /// 权限服务实现
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private static readonly Lazy<PermissionService> _instance =
            new Lazy<PermissionService>(() => new PermissionService());

        private readonly string _usersFilePath;
        private List<User> _users;
        private int _loginFailureCount;

        public static PermissionService Instance => _instance.Value;

        public User CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null;
        public PermissionLevel CurrentLevel => CurrentUser?.Level ?? PermissionLevel.None;

        private PermissionService()
        {
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            _usersFilePath = Path.Combine(configDir, "Users.json");
            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                if (File.Exists(_usersFilePath))
                {
                    var json = File.ReadAllText(_usersFilePath);
                    _users = JsonConvert.DeserializeObject<List<User>>(json) ?? new List<User>();
                }
                else
                {
                    _users = new List<User>();
                }

                // 确保默认管理员存在
                EnsureDefaultAdmin();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"加载用户配置失败: {ex.Message}", ex, "Permission");
                _users = new List<User>();
                EnsureDefaultAdmin();
            }
        }

        private void EnsureDefaultAdmin()
        {
            var defaultAdmin = _users.FirstOrDefault(u => u.IsDefaultAdmin);
            if (defaultAdmin == null)
            {
                _users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = "admin".ToSha256(),
                    Level = PermissionLevel.Administrator,
                    IsDefaultAdmin = true,
                    CreatedTime = DateTime.Now,
                    IsEnabled = true
                });
                SaveUsers();
            }
        }

        private void SaveUsers()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_users, Formatting.Indented);
                File.WriteAllText(_usersFilePath, json);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"保存用户配置失败: {ex.Message}", ex, "Permission");
            }
        }

        public bool Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            var passwordHash = password.ToSha256();
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == passwordHash &&
                u.IsEnabled);

            if (user != null)
            {
                CurrentUser = user;
                user.LastLoginTime = DateTime.Now;
                _loginFailureCount = 0;
                SaveUsers();

                FileLogger.Instance.Info($"用户 {username} 登录成功", "Permission");
                EventAggregator.Instance.Publish(new UserLoginEvent
                {
                    Username = username,
                    PermissionLevel = (int)user.Level,
                    LoginTime = DateTime.Now
                });

                return true;
            }

            _loginFailureCount++;
            FileLogger.Instance.Warning($"用户 {username} 登录失败，尝试次数: {_loginFailureCount}", "Permission");
            return false;
        }

        public void Logout()
        {
            if (CurrentUser != null)
            {
                FileLogger.Instance.Info($"用户 {CurrentUser.Username} 登出", "Permission");
                CurrentUser = null;
            }
        }

        public bool HasPermission(PermissionLevel requiredLevel)
        {
            return CurrentLevel >= requiredLevel;
        }

        public List<User> GetAllUsers()
        {
            return _users.ToList();
        }

        public bool AddUser(string username, string password, PermissionLevel level)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return false;

            _users.Add(new User
            {
                Username = username,
                PasswordHash = password.ToSha256(),
                Level = level,
                IsDefaultAdmin = false,
                CreatedTime = DateTime.Now,
                IsEnabled = true
            });

            SaveUsers();
            FileLogger.Instance.Info($"添加用户 {username}, 权限等级: {level}", "Permission");
            return true;
        }

        public bool DeleteUser(string username)
        {
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null || user.IsDefaultAdmin)
                return false;

            _users.Remove(user);
            SaveUsers();
            FileLogger.Instance.Info($"删除用户 {username}", "Permission");
            return true;
        }

        public bool ChangePassword(string username, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                return false;

            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return false;

            user.PasswordHash = newPassword.ToSha256();
            SaveUsers();
            FileLogger.Instance.Info($"用户 {username} 密码已修改", "Permission");
            return true;
        }

        public bool ChangeUserLevel(string username, PermissionLevel newLevel)
        {
            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return false;

            var oldLevel = user.Level;
            user.Level = newLevel;
            SaveUsers();
            FileLogger.Instance.Info($"用户 {username} 权限从 {oldLevel} 变更为 {newLevel}", "Permission");
            return true;
        }
    }
}
