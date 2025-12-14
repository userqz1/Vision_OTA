namespace VisionOTA.Infrastructure.Permission
{
    /// <summary>
    /// 权限等级
    /// </summary>
    public enum PermissionLevel
    {
        /// <summary>
        /// 无权限/未登录
        /// </summary>
        None = 0,

        /// <summary>
        /// 操作员 - 基本操作权限
        /// </summary>
        Operator = 1,

        /// <summary>
        /// 工程师 - 参数调整权限
        /// </summary>
        Engineer = 2,

        /// <summary>
        /// 管理员 - 完全权限
        /// </summary>
        Administrator = 3
    }
}
