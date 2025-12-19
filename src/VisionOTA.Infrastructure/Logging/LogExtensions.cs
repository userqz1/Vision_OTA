using System;

namespace VisionOTA.Infrastructure.Logging
{
    /// <summary>
    /// 日志扩展方法 - 简化日志调用
    /// </summary>
    public static class LogExtensions
    {
        /// <summary>
        /// 记录调试日志
        /// </summary>
        public static void LogDebug(this object source, string message)
        {
            FileLogger.Instance.Debug(message, GetCategory(source));
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void LogInfo(this object source, string message)
        {
            FileLogger.Instance.Info(message, GetCategory(source));
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void LogWarning(this object source, string message)
        {
            FileLogger.Instance.Warning(message, GetCategory(source));
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void LogError(this object source, string message, Exception ex = null)
        {
            FileLogger.Instance.Error(message, ex, GetCategory(source));
        }

        /// <summary>
        /// 获取分类名称
        /// </summary>
        private static string GetCategory(object source)
        {
            if (source == null) return "Unknown";
            if (source is string s) return s;
            if (source is Type t) return t.Name;
            return source.GetType().Name;
        }
    }
}
