using System.Collections.Generic;

namespace VisionOTA.Infrastructure.Permission
{
    /// <summary>
    /// 权限服务接口
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 当前登录用户
        /// </summary>
        User CurrentUser { get; }

        /// <summary>
        /// 是否已登录
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 当前权限等级
        /// </summary>
        PermissionLevel CurrentLevel { get; }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>是否成功</returns>
        bool Login(string username, string password);

        /// <summary>
        /// 用户登出
        /// </summary>
        void Logout();

        /// <summary>
        /// 检查是否有指定权限
        /// </summary>
        /// <param name="requiredLevel">所需权限等级</param>
        /// <returns>是否有权限</returns>
        bool HasPermission(PermissionLevel requiredLevel);

        /// <summary>
        /// 获取所有用户
        /// </summary>
        /// <returns>用户列表</returns>
        List<User> GetAllUsers();

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="level">权限等级</param>
        /// <returns>是否成功</returns>
        bool AddUser(string username, string password, PermissionLevel level);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>是否成功</returns>
        bool DeleteUser(string username);

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>是否成功</returns>
        bool ChangePassword(string username, string newPassword);

        /// <summary>
        /// 修改用户权限
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="newLevel">新权限等级</param>
        /// <returns>是否成功</returns>
        bool ChangeUserLevel(string username, PermissionLevel newLevel);
    }
}
