using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Camera;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 工位视图模型 - 封装单个工位的相机和统计数据
    /// </summary>
    public class StationViewModel : ViewModelBase
    {
        private readonly int _stationId;
        private ICamera _camera;
        private readonly Func<ICamera> _cameraFactory;

        #region 私有字段

        private string _userId;
        private string _friendlyName = "--";
        private string _serialDisplay = "--";
        private string _statusText = "未连接";
        private int _exposure;
        private double _gain;
        private string _triggerSource = "连续采集";
        private BitmapSource _image;
        private SolidColorBrush _statusColor = new SolidColorBrush(Colors.Gray);

        // 软触发定时器
        private System.Timers.Timer _softTriggerTimer;
        private const int SOFT_TRIGGER_INTERVAL_MS = 100; // 软触发间隔100ms

        // 线扫相机特有参数
        private int _lineRate = 10000;
        private int _lineCount = 1024;

        // 统计数据
        private int _total;
        private int _okCount;
        private int _ngCount;
        private double _okRate;

        // 最新结果
        private string _resultText = "--";
        private SolidColorBrush _resultBackground = new SolidColorBrush(Colors.Gray);
        private double _angle;

        #endregion

        #region 属性

        public int StationId => _stationId;
        public string StationName => _stationId == 1 ? "瓶底定位" : "瓶身定位";
        public string CameraType => _stationId == 1 ? "面阵相机" : "线扫相机";
        public bool IsLineCamera => _stationId == 2;

        public string UserId
        {
            get => _userId;
            set => SetProperty(ref _userId, value);
        }

        public string FriendlyName
        {
            get => _friendlyName;
            set => SetProperty(ref _friendlyName, value);
        }

        public string SerialDisplay
        {
            get => _serialDisplay;
            set => SetProperty(ref _serialDisplay, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int Exposure
        {
            get => _exposure;
            set
            {
                if (SetProperty(ref _exposure, value))
                    ApplyExposure();
            }
        }

        public double Gain
        {
            get => _gain;
            set
            {
                if (SetProperty(ref _gain, value))
                    ApplyGain();
            }
        }

        /// <summary>
        /// 触发源：连续采集、软件触发、Line0、Line1、Line2、Line3
        /// </summary>
        public string TriggerSource
        {
            get => _triggerSource;
            set
            {
                if (SetProperty(ref _triggerSource, value))
                {
                    OnPropertyChanged(nameof(IsSoftwareTrigger));
                    ApplyTriggerSource();
                }
            }
        }

        /// <summary>
        /// 是否为软件触发模式
        /// </summary>
        public bool IsSoftwareTrigger => TriggerSource == "软件触发";

        public BitmapSource Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public bool IsConnected => _camera?.IsConnected == true;
        public bool IsGrabbing => _camera?.IsGrabbing == true;

        // 线扫相机特有属性
        public int LineRate
        {
            get => _lineRate;
            set
            {
                if (SetProperty(ref _lineRate, value))
                    ApplyLineRate();
            }
        }

        public int LineCount
        {
            get => _lineCount;
            set
            {
                if (SetProperty(ref _lineCount, value))
                    ApplyLineCount();
            }
        }

        // 统计属性
        public int Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }

        public int OkCount
        {
            get => _okCount;
            set => SetProperty(ref _okCount, value);
        }

        public int NgCount
        {
            get => _ngCount;
            set => SetProperty(ref _ngCount, value);
        }

        public double OkRate
        {
            get => _okRate;
            set => SetProperty(ref _okRate, value);
        }

        // 最新结果
        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        public SolidColorBrush ResultBackground
        {
            get => _resultBackground;
            set => SetProperty(ref _resultBackground, value);
        }

        public double Angle
        {
            get => _angle;
            set => SetProperty(ref _angle, value);
        }

        #endregion

        #region 按钮文字

        public string ConnectionButtonText => IsConnected ? "断开" : "连接";
        public string GrabButtonText => IsGrabbing ? "停止采集" : "开始采集";

        #endregion

        #region 命令

        public ICommand ToggleConnectionCommand { get; }
        public ICommand ToggleGrabCommand { get; }
        public ICommand SaveImageCommand { get; }

        #endregion

        #region 事件

        public event EventHandler<ImageReceivedEventArgs> ImageReceived;

        #endregion

        public StationViewModel(int stationId, Func<ICamera> cameraFactory)
        {
            _stationId = stationId;
            _cameraFactory = cameraFactory;

            ToggleConnectionCommand = new RelayCommand(_ => ToggleConnection());
            ToggleGrabCommand = new RelayCommand(_ => ToggleGrab(), _ => IsConnected);
            SaveImageCommand = new RelayCommand(_ => SaveImage(), _ => Image != null);
        }

        private void ToggleConnection()
        {
            if (IsConnected)
                Disconnect();
            else
                Connect();
        }

        private void ToggleGrab()
        {
            if (IsGrabbing)
                StopGrab();
            else
                StartGrab();
        }

        #region 相机操作

        public void Connect()
        {
            try
            {
                if (_camera == null)
                {
                    _camera = _cameraFactory();
                    _camera.ImageReceived += OnImageReceived;
                    _camera.ConnectionChanged += OnConnectionChanged;
                }

                if (_camera.Connect(UserId))
                {
                    StatusText = "已连接";
                    StatusColor = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                    FriendlyName = _camera.FriendlyName;
                    SerialDisplay = _camera.SerialNumber ?? "--";
                    FileLogger.Instance.Info($"工位{_stationId}相机连接成功: {FriendlyName}", "Camera");
                }
                else
                {
                    StatusText = "连接失败";
                    StatusColor = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                }

                RefreshCommands();
            }
            catch (Exception ex)
            {
                StatusText = "连接异常";
                StatusColor = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                FileLogger.Instance.Error($"工位{_stationId}相机连接失败: {ex.Message}", ex, "Camera");
            }
        }

        public void Disconnect()
        {
            try
            {
                _camera?.Disconnect();
                StatusText = "未连接";
                StatusColor = new SolidColorBrush(Colors.Gray);
                FriendlyName = "--";
                SerialDisplay = "--";
                RefreshCommands();
                FileLogger.Instance.Info($"工位{_stationId}相机已断开", "Camera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}相机断开失败: {ex.Message}", ex, "Camera");
            }
        }

        public void StartGrab()
        {
            FileLogger.Instance.Info($"工位{_stationId} StartGrab被调用, _camera={(_camera == null ? "null" : "有效")}", "Camera");

            if (_camera != null)
            {
                FileLogger.Instance.Info($"工位{_stationId} 调用相机StartGrab, IsConnected={_camera.IsConnected}", "Camera");
                var result = _camera.StartGrab();
                FileLogger.Instance.Info($"工位{_stationId} 相机StartGrab返回: {result}", "Camera");
            }

            // 软件触发模式下，启动定时器循环触发
            if (IsSoftwareTrigger)
            {
                StartSoftTriggerTimer();
            }

            RefreshCommands();
        }

        public void StopGrab()
        {
            // 停止软触发定时器
            StopSoftTriggerTimer();

            _camera?.StopGrab();
            RefreshCommands();
        }

        /// <summary>
        /// 启动软触发定时器
        /// </summary>
        private void StartSoftTriggerTimer()
        {
            if (_softTriggerTimer == null)
            {
                _softTriggerTimer = new System.Timers.Timer(SOFT_TRIGGER_INTERVAL_MS);
                _softTriggerTimer.Elapsed += (s, e) => SoftTrigger();
                _softTriggerTimer.AutoReset = true;
            }
            _softTriggerTimer.Start();
            FileLogger.Instance.Info($"工位{_stationId}启动软触发循环，间隔{SOFT_TRIGGER_INTERVAL_MS}ms", "Camera");
        }

        /// <summary>
        /// 停止软触发定时器
        /// </summary>
        private void StopSoftTriggerTimer()
        {
            if (_softTriggerTimer != null)
            {
                _softTriggerTimer.Stop();
                FileLogger.Instance.Info($"工位{_stationId}停止软触发循环", "Camera");
            }
        }

        private void SoftTrigger()
        {
            _camera?.SoftTrigger();
        }

        #region 参数自动应用

        private void ApplyExposure()
        {
            if (_camera == null || !IsConnected) return;
            try
            {
                _camera.SetExposure(Exposure);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置曝光失败: {ex.Message}", ex, "Camera");
            }
        }

        private void ApplyGain()
        {
            if (_camera == null || !IsConnected) return;
            try
            {
                _camera.SetGain(Gain);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置增益失败: {ex.Message}", ex, "Camera");
            }
        }

        private void ApplyTriggerSource()
        {
            if (_camera == null || !IsConnected) return;
            try
            {
                var source = MapTriggerSource(TriggerSource);
                _camera.SetTriggerSource(source);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置触发源失败: {ex.Message}", ex, "Camera");
            }
        }

        private void ApplyLineRate()
        {
            if (_camera == null || !IsConnected) return;
            if (_camera is ILineCamera lineCamera)
            {
                try
                {
                    lineCamera.SetLineRate(LineRate);
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"工位{_stationId}设置行频失败: {ex.Message}", ex, "Camera");
                }
            }
        }

        private void ApplyLineCount()
        {
            if (_camera == null || !IsConnected) return;
            if (_camera is ILineCamera lineCamera)
            {
                try
                {
                    lineCamera.SetLineCount(LineCount);
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"工位{_stationId}设置行数失败: {ex.Message}", ex, "Camera");
                }
            }
        }

        #endregion

        /// <summary>
        /// 将界面触发源字符串映射为枚举值
        /// </summary>
        private Hardware.Camera.TriggerSource MapTriggerSource(string triggerSource)
        {
            switch (triggerSource)
            {
                case "软件触发":
                    return Hardware.Camera.TriggerSource.Software;
                case "Line0":
                    return Hardware.Camera.TriggerSource.Line0;
                case "Line1":
                    return Hardware.Camera.TriggerSource.Line1;
                case "Line2":
                    return Hardware.Camera.TriggerSource.Line2;
                case "Line3":
                    return Hardware.Camera.TriggerSource.Line3;
                default: // 连续采集
                    return Hardware.Camera.TriggerSource.Continuous;
            }
        }

        private void OnImageReceived(object sender, ImageReceivedEventArgs e)
        {
            Image = ConvertToBitmapSource(e.Image);
            ImageReceived?.Invoke(this, e);
        }

        private void OnConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            if (e.IsConnected)
            {
                StatusText = "已连接";
                StatusColor = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            }
            else
            {
                StatusText = "未连接";
                StatusColor = new SolidColorBrush(Colors.Gray);
            }
            RefreshCommands();
        }

        private void RefreshCommands()
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsGrabbing));
            OnPropertyChanged(nameof(ConnectionButtonText));
            OnPropertyChanged(nameof(GrabButtonText));
            (ToggleConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleGrabCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region 统计更新

        public void UpdateStatistics(int total, int ok, int ng)
        {
            Total = total;
            OkCount = ok;
            NgCount = ng;
            OkRate = total > 0 ? (double)ok / total * 100 : 0;
        }

        public void UpdateResult(bool isOk, double angle)
        {
            ResultText = isOk ? "OK" : "NG";
            ResultBackground = isOk
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
                : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            Angle = angle;
        }

        private void SaveImage()
        {
            if (Image == null) return;

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = $"保存工位{_stationId}图像",
                    Filter = "PNG图像|*.png|JPEG图像|*.jpg|BMP图像|*.bmp",
                    FilterIndex = 1,
                    FileName = $"Station{_stationId}_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
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

                    encoder.Frames.Add(BitmapFrame.Create(Image));

                    using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                    {
                        encoder.Save(stream);
                    }

                    FileLogger.Instance.Info($"工位{_stationId}图像已保存: {dialog.FileName}", "Camera");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}保存图像失败: {ex.Message}", ex, "Camera");
            }
        }

        #endregion

        #region 辅助方法

        private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null) return null;

            try
            {
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                var bitmapSource = BitmapSource.Create(
                    bitmap.Width, bitmap.Height,
                    bitmap.HorizontalResolution, bitmap.VerticalResolution,
                    PixelFormats.Bgr24, null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmap.Height,
                    bitmapData.Stride);

                bitmap.UnlockBits(bitmapData);

                if (bitmapSource.CanFreeze)
                    bitmapSource.Freeze();

                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        public override void Cleanup()
        {
            // 停止并释放软触发定时器
            if (_softTriggerTimer != null)
            {
                _softTriggerTimer.Stop();
                _softTriggerTimer.Dispose();
                _softTriggerTimer = null;
            }

            if (_camera != null)
            {
                _camera.ImageReceived -= OnImageReceived;
                _camera.ConnectionChanged -= OnConnectionChanged;
                _camera.Dispose();
                _camera = null;
            }
            base.Cleanup();
        }
    }
}
