using System;

namespace VisionOTA.Infrastructure.Logging
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// 日志接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录调试日志
        /// </summary>
        void Debug(string message, string source = null);

        /// <summary>
        /// 记录信息日志
        /// </summary>
        void Info(string message, string source = null);

        /// <summary>
        /// 记录警告日志
        /// </summary>
        void Warning(string message, string source = null);

        /// <summary>
        /// 记录错误日志
        /// </summary>
        void Error(string message, string source = null);

        /// <summary>
        /// 记录错误日志(带异常)
        /// </summary>
        void Error(string message, Exception exception, string source = null);

        /// <summary>
        /// 记录致命错误日志
        /// </summary>
        void Fatal(string message, string source = null);

        /// <summary>
        /// 记录致命错误日志(带异常)
        /// </summary>
        void Fatal(string message, Exception exception, string source = null);
    }
}
