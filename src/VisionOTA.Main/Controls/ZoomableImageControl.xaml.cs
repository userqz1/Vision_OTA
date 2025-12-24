using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VisionOTA.Main.Controls
{
    /// <summary>
    /// 可缩放图像控件 - 支持缩放、平移、辅助线
    /// </summary>
    public partial class ZoomableImageControl : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 图像源
        /// </summary>
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(object), typeof(ZoomableImageControl),
                new PropertyMetadata(null, OnImageSourceChanged));

        public object ImageSource
        {
            get => GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        /// <summary>
        /// 是否显示辅助线
        /// </summary>
        public static readonly DependencyProperty ShowCrosshairProperty =
            DependencyProperty.Register(nameof(ShowCrosshair), typeof(bool), typeof(ZoomableImageControl),
                new PropertyMetadata(false, OnShowCrosshairChanged));

        public bool ShowCrosshair
        {
            get => (bool)GetValue(ShowCrosshairProperty);
            set => SetValue(ShowCrosshairProperty, value);
        }

        /// <summary>
        /// 辅助线可见性（内部使用）
        /// </summary>
        public Visibility CrosshairVisibility => ShowCrosshair ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #region 私有字段

        private const double MinZoom = 0.1;
        private const double MaxZoom = 20.0;
        private const double ZoomStep = 1.2;

        private double _currentZoom = 1.0;
        private System.Windows.Point _lastMousePos;
        private bool _isPanning;
        private BitmapSource _currentBitmap;
        private int _imageWidth;
        private int _imageHeight;
        private bool _hasInitialFit; // 是否已初始化适应窗口

        // FPS计算
        private int _frameCount;
        private DateTime _lastFpsTime = DateTime.Now;
        private DateTime _lastFrameTime = DateTime.Now;
        private double _currentFps;
        private double _instantFps; // 瞬时帧率（用于首帧立即显示）

        #endregion

        public ZoomableImageControl()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后，如果有图像则自适应显示
            if (_currentBitmap != null && !_hasInitialFit)
            {
                _hasInitialFit = true;
                FitToWindow();
            }
        }

        #region 图像源处理

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ZoomableImageControl)d;
            control.UpdateImage(e.NewValue);
        }

        private void UpdateImage(object source)
        {
            _currentBitmap = ConvertToBitmapSource(source);

            if (_currentBitmap != null)
            {
                var newWidth = _currentBitmap.PixelWidth;
                var newHeight = _currentBitmap.PixelHeight;
                var sizeChanged = (newWidth != _imageWidth || newHeight != _imageHeight);

                _imageWidth = newWidth;
                _imageHeight = newHeight;

                // 使用冻结的位图提高性能
                if (_currentBitmap.CanFreeze && !_currentBitmap.IsFrozen)
                {
                    _currentBitmap.Freeze();
                }

                DisplayImage.Source = _currentBitmap;

                // 计算FPS
                UpdateFps();

                // 更新状态栏
                TxtImageSize.Text = $"{_imageWidth} × {_imageHeight}";

                // 只在首次或尺寸变化时自动适应窗口
                // 注意：_hasInitialFit 在 FitToWindow 成功后才设置，确保失败时可以重试
                if (!_hasInitialFit || sizeChanged)
                {
                    Dispatcher.BeginInvoke(new Action(FitToWindow), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            else
            {
                DisplayImage.Source = null;
                TxtImageSize.Text = "";
                TxtFps.Text = "--";
                _imageWidth = 0;
                _imageHeight = 0;
                _hasInitialFit = false;
                // 重置FPS计数
                _frameCount = 0;
                _currentFps = 0;
                _lastFpsTime = DateTime.Now;
                _lastFrameTime = DateTime.Now;
            }
        }

        private void UpdateFps()
        {
            var now = DateTime.Now;

            // 计算瞬时帧率（帧间隔的倒数）
            var frameInterval = (now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;
            if (frameInterval > 0 && frameInterval < 10) // 忽略异常大的间隔
            {
                _instantFps = 1.0 / frameInterval;
            }

            _frameCount++;
            var elapsed = (now - _lastFpsTime).TotalSeconds;

            // 首帧立即显示瞬时帧率
            if (_frameCount == 1 && TxtFps != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TxtFps.Text = "...";
                }));
            }

            // 每秒更新一次平均帧率
            if (elapsed >= 1.0)
            {
                _currentFps = _frameCount / elapsed;
                _frameCount = 0;
                _lastFpsTime = now;

                // 更新FPS显示
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (TxtFps != null)
                        TxtFps.Text = $"{_currentFps:F1}";
                }));
            }
            // 0.5秒时也更新一次，让用户更快看到帧率
            else if (elapsed >= 0.5 && _frameCount > 0 && _currentFps == 0)
            {
                var tempFps = _frameCount / elapsed;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (TxtFps != null)
                        TxtFps.Text = $"{tempFps:F1}";
                }));
            }
        }

        /// <summary>
        /// 转换各种格式为BitmapSource
        /// </summary>
        private BitmapSource ConvertToBitmapSource(object source)
        {
            if (source == null) return null;

            // 已经是BitmapSource
            if (source is BitmapSource bitmapSource)
            {
                return bitmapSource;
            }

            // System.Drawing.Bitmap
            if (source is Bitmap bitmap)
            {
                return ConvertBitmapToBitmapSource(bitmap);
            }

            // 字节数组
            if (source is byte[] bytes)
            {
                return LoadFromBytes(bytes);
            }

            // 文件路径
            if (source is string path && File.Exists(path))
            {
                return LoadFromFile(path);
            }

            // ImageSource
            if (source is ImageSource imageSource && imageSource is BitmapSource bs)
            {
                return bs;
            }

            return null;
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly, bitmap.PixelFormat);

                var pixelFormat = GetWpfPixelFormat(bitmap.PixelFormat);
                // 使用标准96 DPI，与StationViewModel保持一致
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
                // 备用方法：使用内存流
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Bmp);
                    memory.Position = 0;
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = memory;
                    bi.EndInit();
                    return bi;
                }
            }
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
                case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
                    return PixelFormats.Gray16;
                default:
                    return PixelFormats.Bgr24;
            }
        }

        private BitmapSource LoadFromBytes(byte[] bytes)
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

        private BitmapSource LoadFromFile(string path)
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            return bi;
        }

        #endregion

        #region 缩放和平移

        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentBitmap == null) return;

            var mousePos = e.GetPosition(ImageContainer);
            var oldZoom = _currentZoom;

            // 计算新的缩放比例
            if (e.Delta > 0)
                _currentZoom = Math.Min(_currentZoom * ZoomStep, MaxZoom);
            else
                _currentZoom = Math.Max(_currentZoom / ZoomStep, MinZoom);

            // 以鼠标位置为中心缩放
            var zoomRatio = _currentZoom / oldZoom;
            var offsetX = TranslateTransform.X - (mousePos.X - TranslateTransform.X) * (zoomRatio - 1);
            var offsetY = TranslateTransform.Y - (mousePos.Y - TranslateTransform.Y) * (zoomRatio - 1);

            ApplyTransform(_currentZoom, offsetX, offsetY);
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

            // 更新坐标显示
            UpdateCoordinateDisplay(currentPos);

            // 平移
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = currentPos - _lastMousePos;
                var newX = TranslateTransform.X + delta.X;
                var newY = TranslateTransform.Y + delta.Y;

                ApplyTransform(_currentZoom, newX, newY);
                _lastMousePos = currentPos;
            }
        }

        private void ImageContainer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键菜单由Grid.ContextMenu处理
        }

        private void UpdateCoordinateDisplay(System.Windows.Point screenPos)
        {
            // 将屏幕坐标转换为图像坐标
            var imageX = (screenPos.X - TranslateTransform.X) / _currentZoom;
            var imageY = (screenPos.Y - TranslateTransform.Y) / _currentZoom;

            if (imageX >= 0 && imageX < _imageWidth && imageY >= 0 && imageY < _imageHeight)
            {
                TxtX.Text = ((int)imageX).ToString();
                TxtY.Text = ((int)imageY).ToString();

                // 获取像素值
                TxtPixel.Text = GetPixelValue((int)imageX, (int)imageY);
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
            if (_currentBitmap == null || x < 0 || y < 0 || x >= _imageWidth || y >= _imageHeight)
                return "-";

            try
            {
                // 读取单个像素
                var stride = (_imageWidth * _currentBitmap.Format.BitsPerPixel + 7) / 8;
                var pixelData = new byte[4];
                var rect = new Int32Rect(x, y, 1, 1);

                _currentBitmap.CopyPixels(rect, pixelData, stride, 0);

                if (_currentBitmap.Format == PixelFormats.Gray8)
                {
                    return pixelData[0].ToString();
                }
                else if (_currentBitmap.Format == PixelFormats.Bgr24 || _currentBitmap.Format == PixelFormats.Bgr32)
                {
                    return $"R:{pixelData[2]} G:{pixelData[1]} B:{pixelData[0]}";
                }
                else if (_currentBitmap.Format == PixelFormats.Bgra32)
                {
                    return $"R:{pixelData[2]} G:{pixelData[1]} B:{pixelData[0]}";
                }
                else
                {
                    return pixelData[0].ToString();
                }
            }
            catch
            {
                return "-";
            }
        }

        private void ApplyTransform(double zoom, double offsetX, double offsetY)
        {
            ScaleTransform.ScaleX = zoom;
            ScaleTransform.ScaleY = zoom;
            TranslateTransform.X = offsetX;
            TranslateTransform.Y = offsetY;

            TxtZoom.Text = $"{(int)(zoom * 100)}%";

            // 根据缩放比例动态切换渲染模式
            // >= 100%: NearestNeighbor (显示像素块，适合检查细节)
            // < 100%: HighQuality (平滑显示，适合整体查看)
            if (zoom >= 1.0)
            {
                RenderOptions.SetBitmapScalingMode(DisplayImage, BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                RenderOptions.SetBitmapScalingMode(DisplayImage, BitmapScalingMode.HighQuality);
            }

            // 更新辅助线
            UpdateCrosshair();
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

            if (ShowCrosshair)
            {
                UpdateCrosshair();
            }
        }

        private void UpdateCrosshair()
        {
            if (!ShowCrosshair || _currentBitmap == null) return;

            var containerWidth = ImageContainer.ActualWidth;
            var containerHeight = ImageContainer.ActualHeight;

            // 水平中心线
            HorizontalLine.X1 = 0;
            HorizontalLine.Y1 = containerHeight / 2;
            HorizontalLine.X2 = containerWidth;
            HorizontalLine.Y2 = containerHeight / 2;

            // 垂直中心线
            VerticalLine.X1 = containerWidth / 2;
            VerticalLine.Y1 = 0;
            VerticalLine.X2 = containerWidth / 2;
            VerticalLine.Y2 = containerHeight;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCrosshair();
        }

        #endregion

        #region 按钮和菜单事件

        private void BtnFit_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void Btn100_Click(object sender, RoutedEventArgs e)
        {
            ZoomTo100Percent();
        }

        private void BtnCrosshair_Click(object sender, RoutedEventArgs e)
        {
            ShowCrosshair = BtnCrosshair.IsChecked == true;
        }

        private void MenuFit_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void Menu100_Click(object sender, RoutedEventArgs e)
        {
            ZoomTo100Percent();
        }

        private void MenuCrosshair_Click(object sender, RoutedEventArgs e)
        {
            ShowCrosshair = MenuCrosshair.IsChecked;
        }

        private void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            SaveImage();
        }

        #endregion

        #region 公共方法

        private int _fitRetryCount = 0;
        private const int MAX_FIT_RETRY = 5;

        /// <summary>
        /// 适应窗口显示
        /// </summary>
        public void FitToWindow()
        {
            if (_currentBitmap == null || _imageWidth == 0 || _imageHeight == 0) return;

            var containerWidth = ImageContainer.ActualWidth;
            var containerHeight = ImageContainer.ActualHeight;

            // 如果容器尺寸还没准备好，延迟执行（最多重试5次）
            if (containerWidth <= 10 || containerHeight <= 10)
            {
                if (_fitRetryCount < MAX_FIT_RETRY)
                {
                    _fitRetryCount++;
                    Dispatcher.BeginInvoke(new Action(FitToWindow),
                        System.Windows.Threading.DispatcherPriority.Background);
                }
                return;
            }

            _fitRetryCount = 0;

            var scaleX = containerWidth / _imageWidth;
            var scaleY = containerHeight / _imageHeight;
            _currentZoom = Math.Min(scaleX, scaleY) * 0.98; // 留2%边距

            // 确保缩放比例合理
            if (_currentZoom <= 0 || double.IsNaN(_currentZoom) || double.IsInfinity(_currentZoom))
            {
                _currentZoom = 1.0;
            }

            // 居中显示
            var offsetX = (containerWidth - _imageWidth * _currentZoom) / 2;
            var offsetY = (containerHeight - _imageHeight * _currentZoom) / 2;

            ApplyTransform(_currentZoom, offsetX, offsetY);

            // 适应成功后才标记，确保失败时下一帧可以重试
            _hasInitialFit = true;
        }

        /// <summary>
        /// 1:1显示（居中）
        /// </summary>
        public void ZoomTo100Percent()
        {
            if (_currentBitmap == null) return;

            _currentZoom = 1.0;

            var containerWidth = ImageContainer.ActualWidth;
            var containerHeight = ImageContainer.ActualHeight;

            // 居中显示
            var offsetX = (containerWidth - _imageWidth) / 2;
            var offsetY = (containerHeight - _imageHeight) / 2;

            ApplyTransform(_currentZoom, offsetX, offsetY);
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
    }
}
