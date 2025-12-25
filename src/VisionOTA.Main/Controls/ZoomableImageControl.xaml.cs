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
    /// 可缩放图像控件 - 支持缩放、平移、像素信息显示
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

        public static readonly DependencyProperty ShowCrosshairProperty =
            DependencyProperty.Register(nameof(ShowCrosshair), typeof(bool), typeof(ZoomableImageControl),
                new PropertyMetadata(false, OnShowCrosshairChanged));

        public bool ShowCrosshair
        {
            get => (bool)GetValue(ShowCrosshairProperty);
            set => SetValue(ShowCrosshairProperty, value);
        }

        public Visibility CrosshairVisibility => ShowCrosshair ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #region 私有字段

        private const double MinZoom = 0.01;
        private const double MaxZoom = 50.0;
        private const double ZoomFactor = 1.2;

        private BitmapSource _currentBitmap;
        private int _imageWidth;
        private int _imageHeight;

        // 变换矩阵
        private readonly MatrixTransform _transform = new MatrixTransform();
        private System.Windows.Point _lastMousePos;
        private bool _isPanning;

        // FPS计算
        private int _frameCount;
        private DateTime _lastFpsTime = DateTime.Now;

        #endregion

        public ZoomableImageControl()
        {
            InitializeComponent();
            DisplayImage.RenderTransform = _transform;
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_currentBitmap != null)
            {
                FitToWindow();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCrosshair();
        }

        #region 图像源处理

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ZoomableImageControl)d;
            control.UpdateImage(e.NewValue);
        }

        private void UpdateImage(object source)
        {
            var newBitmap = ConvertToBitmapSource(source);

            if (newBitmap != null)
            {
                bool isFirstImage = _currentBitmap == null;
                bool sizeChanged = _imageWidth != newBitmap.PixelWidth || _imageHeight != newBitmap.PixelHeight;

                _imageWidth = newBitmap.PixelWidth;
                _imageHeight = newBitmap.PixelHeight;

                // 冻结位图提高性能
                if (newBitmap.CanFreeze && !newBitmap.IsFrozen)
                {
                    newBitmap.Freeze();
                }

                _currentBitmap = newBitmap;
                DisplayImage.Source = _currentBitmap;

                // 更新状态栏
                TxtImageSize.Text = $"{_imageWidth} x {_imageHeight}";
                UpdateFps();

                // 首次加载或尺寸变化时自动适应窗口
                if (isFirstImage || sizeChanged)
                {
                    Dispatcher.BeginInvoke(new Action(FitToWindow), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            else
            {
                _currentBitmap = null;
                DisplayImage.Source = null;
                _imageWidth = 0;
                _imageHeight = 0;
                TxtImageSize.Text = "";
                TxtFps.Text = "--";
                ResetTransform();
            }
        }

        private void UpdateFps()
        {
            _frameCount++;
            var elapsed = (DateTime.Now - _lastFpsTime).TotalSeconds;

            if (elapsed >= 1.0)
            {
                double fps = _frameCount / elapsed;
                TxtFps.Text = $"{fps:F1}";
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
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly, bitmap.PixelFormat);

                    var pixelFormat = GetWpfPixelFormat(bitmap.PixelFormat);
                    var bitmapSource = BitmapSource.Create(
                        bitmap.Width, bitmap.Height,
                        96, 96,
                        pixelFormat, null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmap.Height,
                        bitmapData.Stride);

                    bitmap.UnlockBits(bitmapData);
                    return bitmapSource;
                }
                catch
                {
                    return null;
                }
            }

            if (source is byte[] bytes)
            {
                using (var memory = new MemoryStream(bytes))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = memory;
                    bi.EndInit();
                    return bi;
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

        private System.Windows.Media.PixelFormat GetWpfPixelFormat(System.Drawing.Imaging.PixelFormat format)
        {
            switch (format)
            {
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return PixelFormats.Bgr24;
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    return PixelFormats.Bgra32;
                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    return PixelFormats.Bgr32;
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    return PixelFormats.Gray8;
                default:
                    return PixelFormats.Bgr24;
            }
        }

        #endregion

        #region 缩放和平移

        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentBitmap == null) return;

            double scale = e.Delta > 0 ? ZoomFactor : 1.0 / ZoomFactor;
            var mousePos = e.GetPosition(ImageContainer);

            // 获取当前矩阵
            var matrix = _transform.Matrix;

            // 检查缩放限制
            double newScale = matrix.M11 * scale;
            if (newScale < MinZoom || newScale > MaxZoom)
                return;

            // 以鼠标位置为中心缩放
            matrix.ScaleAt(scale, scale, mousePos.X, mousePos.Y);
            _transform.Matrix = matrix;

            UpdateZoomDisplay();
            UpdateBitmapScalingMode();
            e.Handled = true;
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentBitmap == null) return;

            _isPanning = true;
            _lastMousePos = e.GetPosition(ImageContainer);
            ImageContainer.CaptureMouse();
            Cursor = Cursors.Hand;
        }

        private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            ImageContainer.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_currentBitmap == null) return;

            var currentPos = e.GetPosition(ImageContainer);

            // 更新像素坐标和值
            UpdatePixelInfo(currentPos);

            // 平移
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = currentPos - _lastMousePos;
                var matrix = _transform.Matrix;
                matrix.Translate(delta.X, delta.Y);
                _transform.Matrix = matrix;
                _lastMousePos = currentPos;
            }
        }

        private void ImageContainer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键菜单由ContextMenu处理
        }

        private void UpdatePixelInfo(System.Windows.Point screenPos)
        {
            if (_currentBitmap == null)
            {
                TxtX.Text = "-";
                TxtY.Text = "-";
                TxtPixel.Text = "-";
                return;
            }

            // 将屏幕坐标转换为图像坐标
            var matrix = _transform.Matrix;
            matrix.Invert();
            var imagePoint = matrix.Transform(screenPos);

            int x = (int)imagePoint.X;
            int y = (int)imagePoint.Y;

            if (x >= 0 && x < _imageWidth && y >= 0 && y < _imageHeight)
            {
                TxtX.Text = x.ToString();
                TxtY.Text = y.ToString();
                TxtPixel.Text = GetPixelValue(x, y);
            }
            else
            {
                TxtX.Text = "-";
                TxtY.Text = "-";
                TxtPixel.Text = "-";
            }
        }

        private string GetPixelValue(int x, int y)
        {
            if (_currentBitmap == null) return "-";

            try
            {
                var format = _currentBitmap.Format;
                int bytesPerPixel = (format.BitsPerPixel + 7) / 8;
                int stride = _imageWidth * bytesPerPixel;
                byte[] pixels = new byte[bytesPerPixel];

                _currentBitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, stride, 0);

                if (format == PixelFormats.Gray8)
                {
                    // 灰度图：显示 R:xxx G:xxx B:xxx (相同值)
                    int gray = pixels[0];
                    return $"R:{gray} G:{gray} B:{gray}";
                }
                else if (format == PixelFormats.Bgr24 || format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32)
                {
                    return $"R:{pixels[2]} G:{pixels[1]} B:{pixels[0]}";
                }
                else
                {
                    return pixels[0].ToString();
                }
            }
            catch
            {
                return "-";
            }
        }

        private void UpdateZoomDisplay()
        {
            double zoom = _transform.Matrix.M11 * 100;
            TxtZoom.Text = $"{zoom:F0}%";
        }

        private void UpdateBitmapScalingMode()
        {
            double zoom = _transform.Matrix.M11;
            // >= 100% 显示像素块，< 100% 平滑显示
            var mode = zoom >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
            RenderOptions.SetBitmapScalingMode(DisplayImage, mode);
        }

        private void ResetTransform()
        {
            _transform.Matrix = Matrix.Identity;
            UpdateZoomDisplay();
        }

        #endregion

        #region 辅助线

        private static void OnShowCrosshairChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ZoomableImageControl)d;
            control.UpdateCrosshairVisibility();
        }

        private void UpdateCrosshairVisibility()
        {
            CrosshairCanvas.Visibility = ShowCrosshair ? Visibility.Visible : Visibility.Collapsed;
            BtnCrosshair.IsChecked = ShowCrosshair;
            MenuCrosshair.IsChecked = ShowCrosshair;
            if (ShowCrosshair) UpdateCrosshair();
        }

        private void UpdateCrosshair()
        {
            if (!ShowCrosshair) return;

            double w = ImageContainer.ActualWidth;
            double h = ImageContainer.ActualHeight;

            HorizontalLine.X1 = 0;
            HorizontalLine.Y1 = h / 2;
            HorizontalLine.X2 = w;
            HorizontalLine.Y2 = h / 2;

            VerticalLine.X1 = w / 2;
            VerticalLine.Y1 = 0;
            VerticalLine.X2 = w / 2;
            VerticalLine.Y2 = h;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 自动适应窗口（图像填满画布）
        /// </summary>
        public void FitToWindow()
        {
            if (_currentBitmap == null || _imageWidth == 0 || _imageHeight == 0) return;

            double containerWidth = ImageContainer.ActualWidth;
            double containerHeight = ImageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
            {
                // 容器尚未准备好，延迟执行
                Dispatcher.BeginInvoke(new Action(FitToWindow), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            // 计算缩放比例使图像填满画布
            double scaleX = containerWidth / _imageWidth;
            double scaleY = containerHeight / _imageHeight;
            double scale = Math.Min(scaleX, scaleY);

            // 计算居中偏移
            double offsetX = (containerWidth - _imageWidth * scale) / 2;
            double offsetY = (containerHeight - _imageHeight * scale) / 2;

            // 设置变换矩阵
            var matrix = new Matrix();
            matrix.Scale(scale, scale);
            matrix.Translate(offsetX / scale, offsetY / scale);

            // 重新计算：先缩放后平移
            matrix = new Matrix();
            matrix.Scale(scale, scale);
            matrix.OffsetX = offsetX;
            matrix.OffsetY = offsetY;

            _transform.Matrix = matrix;

            UpdateZoomDisplay();
            UpdateBitmapScalingMode();
        }

        /// <summary>
        /// 1:1显示（居中）
        /// </summary>
        public void ZoomTo100Percent()
        {
            if (_currentBitmap == null) return;

            double containerWidth = ImageContainer.ActualWidth;
            double containerHeight = ImageContainer.ActualHeight;

            double offsetX = (containerWidth - _imageWidth) / 2;
            double offsetY = (containerHeight - _imageHeight) / 2;

            var matrix = new Matrix();
            matrix.OffsetX = offsetX;
            matrix.OffsetY = offsetY;

            _transform.Matrix = matrix;

            UpdateZoomDisplay();
            UpdateBitmapScalingMode();
        }

        /// <summary>
        /// 保存图片
        /// </summary>
        public void SaveImage()
        {
            if (_currentBitmap == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "保存图片",
                Filter = "PNG图片|*.png|JPEG图片|*.jpg|BMP图片|*.bmp",
                DefaultExt = ".png",
                FileName = $"Image_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    BitmapEncoder encoder;
                    var ext = Path.GetExtension(dialog.FileName).ToLower();

                    switch (ext)
                    {
                        case ".jpg":
                        case ".jpeg":
                            encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                            break;
                        case ".bmp":
                            encoder = new BmpBitmapEncoder();
                            break;
                        default:
                            encoder = new PngBitmapEncoder();
                            break;
                    }

                    encoder.Frames.Add(BitmapFrame.Create(_currentBitmap));

                    using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                    {
                        encoder.Save(stream);
                    }

                    MessageBox.Show($"图片已保存: {dialog.FileName}", "保存成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region 按钮事件

        private void BtnFit_Click(object sender, RoutedEventArgs e) => FitToWindow();
        private void Btn100_Click(object sender, RoutedEventArgs e) => ZoomTo100Percent();
        private void BtnCrosshair_Click(object sender, RoutedEventArgs e) => ShowCrosshair = BtnCrosshair.IsChecked == true;
        private void MenuFit_Click(object sender, RoutedEventArgs e) => FitToWindow();
        private void Menu100_Click(object sender, RoutedEventArgs e) => ZoomTo100Percent();
        private void MenuCrosshair_Click(object sender, RoutedEventArgs e) => ShowCrosshair = MenuCrosshair.IsChecked;
        private void MenuSave_Click(object sender, RoutedEventArgs e) => SaveImage();

        #endregion
    }
}
