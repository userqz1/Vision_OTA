using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VisionOTA.Main.Converters
{
    /// <summary>
    /// 字符串到可见性转换器：非空字符串显示，空字符串隐藏
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
