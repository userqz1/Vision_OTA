using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VisionOTA.Main.Converters
{
    /// <summary>
    /// 状态到颜色转换器
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOk)
            {
                return isOk
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))  // 绿色
                    : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // 红色
            }

            if (value is string status)
            {
                switch (status.ToLower())
                {
                    case "ok":
                    case "running":
                    case "connected":
                        return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // 绿色
                    case "ng":
                    case "error":
                    case "disconnected":
                        return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // 红色
                    case "warning":
                    case "paused":
                        return new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)); // 黄色
                    default:
                        return new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD)); // 灰色
                }
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
