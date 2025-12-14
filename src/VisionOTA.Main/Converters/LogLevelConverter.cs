using System;
using System.Globalization;
using System.Windows.Data;

namespace VisionOTA.Main.Converters
{
    /// <summary>
    /// 日志级别转换器，从日志行提取级别
    /// </summary>
    public class LogLevelConverter : IValueConverter
    {
        public static readonly LogLevelConverter Instance = new LogLevelConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string logLine)
            {
                // 日志格式: [HH:mm:ss.fff] [Level  ] [Source] Message
                if (logLine.Contains("[Error"))
                    return "Error";
                if (logLine.Contains("[Warning"))
                    return "Warning";
                if (logLine.Contains("[Info"))
                    return "Info";
                if (logLine.Contains("[Debug"))
                    return "Debug";
                if (logLine.Contains("[Fatal"))
                    return "Error";
            }
            return "Info";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
