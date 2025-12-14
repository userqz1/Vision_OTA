using System;
using System.Security.Cryptography;
using System.Text;

namespace VisionOTA.Common.Extensions
{
    /// <summary>
    /// 字符串扩展方法
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// 判断字符串是否为空或仅包含空白字符
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// 判断字符串是否非空
        /// </summary>
        public static bool IsNotNullOrEmpty(this string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 计算字符串的SHA256哈希值
        /// </summary>
        public static string ToSha256(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 截取字符串，超出部分用省略号替代
        /// </summary>
        public static string Truncate(this string value, int maxLength, string ellipsis = "...")
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength - ellipsis.Length) + ellipsis;
        }

        /// <summary>
        /// 安全地转换为整数
        /// </summary>
        public static int ToInt(this string value, int defaultValue = 0)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 安全地转换为双精度浮点数
        /// </summary>
        public static double ToDouble(this string value, double defaultValue = 0.0)
        {
            return double.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 格式化文件大小显示
        /// </summary>
        public static string FormatFileSize(this long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
}
