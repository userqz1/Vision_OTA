using System;
using System.Windows;

namespace VisionOTA.Main.Helpers
{
    /// <summary>
    /// 窗口辅助类 - 处理窗口自适应屏幕
    /// </summary>
    public static class WindowHelper
    {
        /// <summary>
        /// 根据屏幕尺寸自适应窗口大小
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="maxWidthRatio">最大宽度占屏幕比例 (0-1)</param>
        /// <param name="maxHeightRatio">最大高度占屏幕比例 (0-1)</param>
        /// <param name="minWidth">最小宽度</param>
        /// <param name="minHeight">最小高度</param>
        public static void AdaptToScreen(Window window,
            double maxWidthRatio = 0.85,
            double maxHeightRatio = 0.85,
            double minWidth = 800,
            double minHeight = 600)
        {
            // 获取主屏幕工作区尺寸（不包括任务栏）
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            // 计算目标尺寸
            var targetWidth = Math.Min(window.Width, screenWidth * maxWidthRatio);
            var targetHeight = Math.Min(window.Height, screenHeight * maxHeightRatio);

            // 应用最小尺寸限制
            window.Width = Math.Max(targetWidth, minWidth);
            window.Height = Math.Max(targetHeight, minHeight);

            // 设置最小尺寸
            window.MinWidth = minWidth;
            window.MinHeight = minHeight;

            // 确保窗口在屏幕范围内
            EnsureOnScreen(window);
        }

        /// <summary>
        /// 确保窗口在屏幕范围内
        /// </summary>
        public static void EnsureOnScreen(Window window)
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            // 如果窗口超出屏幕右边界
            if (window.Left + window.Width > screenWidth)
            {
                window.Left = Math.Max(0, screenWidth - window.Width);
            }

            // 如果窗口超出屏幕下边界
            if (window.Top + window.Height > screenHeight)
            {
                window.Top = Math.Max(0, screenHeight - window.Height);
            }

            // 如果窗口在屏幕左边界外
            if (window.Left < 0)
            {
                window.Left = 0;
            }

            // 如果窗口在屏幕上边界外
            if (window.Top < 0)
            {
                window.Top = 0;
            }
        }

        /// <summary>
        /// 计算适合屏幕的窗口尺寸
        /// </summary>
        /// <param name="preferredWidth">期望宽度</param>
        /// <param name="preferredHeight">期望高度</param>
        /// <param name="maxRatio">最大占屏幕比例</param>
        /// <returns>调整后的尺寸</returns>
        public static (double Width, double Height) CalculateSize(
            double preferredWidth,
            double preferredHeight,
            double maxRatio = 0.85)
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            var width = Math.Min(preferredWidth, screenWidth * maxRatio);
            var height = Math.Min(preferredHeight, screenHeight * maxRatio);

            return (width, height);
        }
    }
}
