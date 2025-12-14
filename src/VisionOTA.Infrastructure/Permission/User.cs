using System;

namespace VisionOTA.Infrastructure.Permission
{
    /// <summary>
    /// 用户实体
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码哈希值
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// 权限等级
        /// </summary>
        public PermissionLevel Level { get; set; }

        /// <summary>
        /// 是否为默认管理员（不可删除）
        /// </summary>
        public bool IsDefaultAdmin { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LastLoginTime { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
