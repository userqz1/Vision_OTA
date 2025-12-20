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
        private string _triggerMode = "连续采集";
        private string _triggerEdge = "上升沿";
        private string _hardwareTriggerSource = "Line1";
        private BitmapSource _image;
        private SolidColorBrush _statusColor = new SolidColorBrush(Colors.Gray);

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
            set => SetProperty(ref _exposure, value);
        }

        public double Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        public string TriggerMode
        {
            get => _triggerMode;
            set
            {
                if (SetProperty(ref _triggerMode, value))
                {
                    OnPropertyChanged(nameof(IsHardwareTrigger));
                }
            }
        }

        public string TriggerEdge
        {
            get => _triggerEdge;
            set => SetProperty(ref _triggerEdge, value);
        }

        public string HardwareTriggerSource
        {
            get => _hardwareTriggerSource;
            set => SetProperty(ref _hardwareTriggerSource, value);
        }

        public bool IsHardwareTrigger => TriggerMode == "硬件触发";

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
            set => SetProperty(ref _lineRate, value);
        }

        public int LineCount
        {
            get => _lineCount;
            set => SetProperty(ref _lineCount, value);
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
        public ICommand SoftTriggerCommand { get; }
        public ICommand ApplyParamsCommand { get; }
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
            SoftTriggerCommand = new RelayCommand(_ => SoftTrigger(), _ => IsConnected);
            ApplyParamsCommand = new RelayCommand(_ => ApplyParams(), _ => IsConnected);
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
            _camera?.StartGrab();
            RefreshCommands();
        }

        public void StopGrab()
        {
            _camera?.StopGrab();
            RefreshCommands();
        }

        public void SoftTrigger()
        {
            _camera?.SoftTrigger();
        }

        public void ApplyParams()
        {
            if (_camera == null) return;

            try
            {
                _camera.SetExposure(Exposure);
                _camera.SetGain(Gain);

                // 设置触发源
                TriggerSource source;
                switch (TriggerMode)
                {
                    case "软件触发":
                        source = TriggerSource.Software;
                        break;
                    case "硬件触发":
                        source = GetTriggerSourceFromLine(HardwareTriggerSource);
                        break;
                    default:
                        source = TriggerSource.Continuous;
                        break;
                }
                _camera.SetTriggerSource(source);

                // 线扫相机特有参数
                if (_camera is ILineCamera lineCamera)
                {
                    lineCamera.SetLineRate(LineRate);
                    lineCamera.SetLineCount(LineCount);
                }

                FileLogger.Instance.Info($"工位{_stationId}相机参数已应用", "Camera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}应用参数失败: {ex.Message}", ex, "Camera");
            }
        }

        private TriggerSource GetTriggerSourceFromLine(string line)
        {
            switch (line)
            {
                case "Line1": return TriggerSource.Line1;
                case "Line2": return TriggerSource.Line2;
                case "Line3": return TriggerSource.Line3;
                default: return TriggerSource.Line1;
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
            (SoftTriggerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ApplyParamsCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
