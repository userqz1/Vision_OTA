using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VisionOTA.Main.Controls
{
    /// <summary>
    /// 可缩放图像控件
    /// </summary>
    public partial class ZoomableImageControl : UserControl
    {
        #region 依赖属性

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(object), typeof(ZoomableImageControl),
                new PropertyMetadata(null, OnImageSourceChanged));

        public object ImageSource
        {
            get => GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        #endregion

        #region 私有字段

        private BitmapSource _bitmap;
        private int _imageWidth;
        private int _imageHeight;

        // 缩放限制
        private const double MinScale = 0.01;
        private const double MaxScale = 50.0;
        private const double ZoomStep = 1.2;

        // 拖拽状态
        private bool _isDragging;
        private System.Windows.Point _dragStart;
        private double _dragStartX;
        private double _dragStartY;

        // FPS计算
        private int _frameCount;
        private DateTime _lastFpsTime = DateTime.Now;

        #endregion

        public ZoomableImageControl()
        {
            InitializeComponent();
        }

        #region 图像更新

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ZoomableImageControl)d).UpdateImage(e.NewValue);
        }

        private void UpdateImage(object source)
        {
            var newBitmap = ConvertToBitmapSource(source);
            if (newBitmap == null)
            {
                _bitmap = null;
                _imageWidth = 0;
                _imageHeight = 0;
                DisplayImage.Source = null;
                TxtSize.Text = "--";
                TxtFps.Text = "--";
                return;
            }

            bool sizeChanged = _imageWidth != newBitmap.PixelWidth || _imageHeight != newBitmap.PixelHeight;
            bool isFirst = _bitmap == null;

            _imageWidth = newBitmap.PixelWidth;
            _imageHeight = newBitmap.PixelHeight;

            // 冻结位图
            if (newBitmap.CanFreeze && !newBitmap.IsFrozen)
                newBitmap.Freeze();

            _bitmap = newBitmap;
            DisplayImage.Source = _bitmap;

            // 更新尺寸显示
            TxtSize.Text = $"{_imageWidth} x {_imageHeight}";

            // 更新FPS
            UpdateFps();

            // 首次或尺寸变化时自动适应
            if (isFirst || sizeChanged)
            {
                Dispatcher.BeginInvoke(new Action(FitToWindow), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void UpdateFps()
        {
            _frameCount++;
            var elapsed = (DateTime.Now - _lastFpsTime).TotalSeconds;
            if (elapsed >= 1.0)
            {
                TxtFps.Text = $"{_frameCount / elapsed:F1}";
                _frameCount = 0;
                _lastFpsTime = DateTime.Now;
            }
        }

        private BitmapSource ConvertToBitmapSource(object source)
        {
            if (source == null) return null;

            if (source is BitmapSource bs)
                return bs;

            if (source is Bitmap bitmap)
            {
                try
                {
                    var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                    var pixelFormat = ConvertPixelFormat(bitmap.PixelFormat);
                    var result = BitmapSource.Create(
                        bitmap.Width, bitmap.Height,
                        96, 96,
                        pixelFormat, null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmap.Height,
                        bitmapData.Stride);

                    bitmap.UnlockBits(bitmapData);
                    return result;
                }
                catch
                {
                    return null;
                }
            }

            if (source is string path && File.Exists(path))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                return bi;
            }

            return null;
        }

        private System.Windows.Media.PixelFormat ConvertPixelFormat(System.Drawing.Imaging.PixelFormat format)
        {
            switch (format)
            {
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    return PixelFormats.Gray8;
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return PixelFormats.Bgr24;
                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    return PixelFormats.Bgr32;
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    return PixelFormats.Bgra32;
                default:
                    return PixelFormats.Bgr24;
            }
        }

        #endregion

        #region 缩放和平移

        private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 容器大小变化时，如果是首次有效尺寸，自动适应
            if (_bitmap != null && e.PreviousSize.Width == 0 && e.NewSize.Width > 0)
            {
                FitToWindow();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_bitmap == null) return;

            var mousePos = e.GetPosition(ImageBorder);
            double factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;

            // 计算新缩放值
            double newScale = ImageScale.ScaleX * factor;
            if (newScale < MinScale || newScale > MaxScale) return;

            // 计算鼠标在图像上的位置（缩放前）
            double imageX = (mousePos.X - ImageTranslate.X) / ImageScale.ScaleX;
            double imageY = (mousePos.Y - ImageTranslate.Y) / ImageScale.ScaleY;

            // 应用新缩放
            ImageScale.ScaleX = newScale;
            ImageScale.ScaleY = newScale;

            // 调整平移使鼠标位置保持不变
            ImageTranslate.X = mousePos.X - imageX * newScale;
            ImageTranslate.Y = mousePos.Y - imageY * newScale;

            UpdateZoomDisplay();
            UpdateScalingMode();
            e.Handled = true;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_bitmap == null) return;

            _isDragging = true;
            _dragStart = e.GetPosition(ImageBorder);
            _dragStartX = ImageTranslate.X;
            _dragStartY = ImageTranslate.Y;
            ImageBorder.CaptureMouse();
            Cursor = Cursors.Hand;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ImageBorder.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_bitmap == null) return;

            var pos = e.GetPosition(ImageBorder);

            // 更新坐标和像素信息
            UpdateCoordAndPixel(pos);

            // 拖拽平移
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                ImageTranslate.X = _dragStartX + (pos.X - _dragStart.X);
                ImageTranslate.Y = _dragStartY + (pos.Y - _dragStart.Y);
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键菜单
        }

        private void UpdateCoordAndPixel(System.Windows.Point screenPos)
        {
            if (_bitmap == null || _imageWidth == 0 || _imageHeight == 0)
            {
                TxtCoord.Text = "(-, -)";
                TxtPixel.Text = "--";
                return;
            }

            // 屏幕坐标转图像坐标
            double scale = ImageScale.ScaleX;
            if (scale <= 0) scale = 1;

            int imageX = (int)((screenPos.X - ImageTranslate.X) / scale);
            int imageY = (int)((screenPos.Y - ImageTranslate.Y) / scale);

            // 检查是否在图像范围内
            if (imageX >= 0 && imageX < _imageWidth && imageY >= 0 && imageY < _imageHeight)
            {
                TxtCoord.Text = $"({imageX}, {imageY})";
                TxtPixel.Text = GetPixelString(imageX, imageY);
            }
            else
            {
                TxtCoord.Text = "(-, -)";
                TxtPixel.Text = "--";
            }
        }

        private string GetPixelString(int x, int y)
        {
            if (_bitmap == null) return "--";

            try
            {
                var format = _bitmap.Format;
                int bpp = (format.BitsPerPixel + 7) / 8;
                byte[] pixel = new byte[bpp];

                _bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, bpp, 0);

                if (format == PixelFormats.Gray8)
                {
                    int g = pixel[0];
                    return $"R:{g} G:{g} B:{g}";
                }
                else if (bpp >= 3)
                {
                    return $"R:{pixel[2]} G:{pixel[1]} B:{pixel[0]}";
                }
                else
                {
                    return pixel[0].ToString();
                }
            }
            catch
            {
                return "--";
            }
        }

        private void UpdateZoomDisplay()
        {
            double zoom = ImageScale.ScaleX * 100;
            TxtZoom.Text = $"{zoom:F0}%";
        }

        private void UpdateScalingMode()
        {
            var mode = ImageScale.ScaleX >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
            RenderOptions.SetBitmapScalingMode(DisplayImage, mode);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 适应窗口
        /// </summary>
        public void FitToWindow()
        {
            if (_bitmap == null || _imageWidth == 0 || _imageHeight == 0) return;

            double containerW = ImageBorder.ActualWidth;
            double containerH = ImageBorder.ActualHeight;

            if (containerW <= 0 || containerH <= 0) return;

            // 计算缩放比例（保持宽高比）
            double scaleX = containerW / _imageWidth;
            double scaleY = containerH / _imageHeight;
            double scale = Math.Min(scaleX, scaleY);

            // 设置缩放
            ImageScale.ScaleX = scale;
            ImageScale.ScaleY = scale;

            // 居中
            double scaledW = _imageWidth * scale;
            double scaledH = _imageHeight * scale;
            ImageTranslate.X = (containerW - scaledW) / 2;
            ImageTranslate.Y = (containerH - scaledH) / 2;

            UpdateZoomDisplay();
            UpdateScalingMode();
        }

        /// <summary>
        /// 1:1显示
        /// </summary>
        public void ZoomTo100()
        {
            if (_bitmap == null) return;

            double containerW = ImageBorder.ActualWidth;
            double containerH = ImageBorder.ActualHeight;

            ImageScale.ScaleX = 1;
            ImageScale.ScaleY = 1;

            // 居中
            ImageTranslate.X = (containerW - _imageWidth) / 2;
            ImageTranslate.Y = (containerH - _imageHeight) / 2;

            UpdateZoomDisplay();
            UpdateScalingMode();
        }

        /// <summary>
        /// 保存图片
        /// </summary>
        public void SaveImage()
        {
            if (_bitmap == null) return;

            var dlg = new SaveFileDialog
            {
                Title = "保存图片",
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
                FileName = $"Image_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    BitmapEncoder encoder;
                    string ext = Path.GetExtension(dlg.FileName).ToLower();

                    if (ext == ".jpg" || ext == ".jpeg")
                        encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                    else if (ext == ".bmp")
                        encoder = new BmpBitmapEncoder();
                    else
                        encoder = new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(_bitmap));

                    using (var fs = new FileStream(dlg.FileName, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show($"已保存: {dlg.FileName}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region 按钮事件

        private void OnFitClick(object sender, RoutedEventArgs e) => FitToWindow();
        private void On100Click(object sender, RoutedEventArgs e) => ZoomTo100();
        private void OnSaveClick(object sender, RoutedEventArgs e) => SaveImage();

        #endregion
    }
}
