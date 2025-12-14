using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Camera;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 相机设置视图模型
    /// </summary>
    public class CameraSettingsViewModel : ViewModelBase
    {
        private ICamera _camera1;
        private ILineCamera _camera2;

        // 工位1
        private string _station1UserId;
        private string _station1FriendlyName = "--";
        private string _station1SerialDisplay = "--";
        private string _station1StatusText = "未连接";
        private int _station1Exposure;
        private double _station1Gain;
        private string _station1TriggerSource;
        private string _station1TriggerEdge;
        private BitmapSource _station1Image;
        private SolidColorBrush _station1StatusColor = new SolidColorBrush(Colors.Gray);
        private CameraInfo _station1SelectedCamera;

        // 工位2
        private string _station2UserId;
        private string _station2FriendlyName = "--";
        private string _station2SerialDisplay = "--";
        private string _station2StatusText = "未连接";
        private int _station2Exposure;
        private double _station2Gain;
        private int _station2LineRate;
        private int _station2LineCount;
        private string _station2TriggerSource;
        private string _station2TriggerEdge;
        private BitmapSource _station2Image;
        private SolidColorBrush _station2StatusColor = new SolidColorBrush(Colors.Gray);
        private CameraInfo _station2SelectedCamera;

        public List<string> TriggerSources { get; } = new List<string>
        {
            "Continuous", "Software", "Line0", "Line1", "Line2", "Line3"
        };

        public List<string> TriggerEdges { get; } = new List<string>
        {
            "上升沿", "下降沿", "双边沿"
        };

        public ObservableCollection<CameraInfo> Station1Cameras { get; } = new ObservableCollection<CameraInfo>();
        public ObservableCollection<CameraInfo> Station2Cameras { get; } = new ObservableCollection<CameraInfo>();

        public event EventHandler<bool> RequestClose;

        #region Station1 Properties

        public string Station1UserId
        {
            get => _station1UserId;
            set => SetProperty(ref _station1UserId, value);
        }

        public string Station1FriendlyName
        {
            get => _station1FriendlyName;
            set => SetProperty(ref _station1FriendlyName, value);
        }

        public string Station1SerialDisplay
        {
            get => _station1SerialDisplay;
            set => SetProperty(ref _station1SerialDisplay, value);
        }

        public string Station1StatusText
        {
            get => _station1StatusText;
            set => SetProperty(ref _station1StatusText, value);
        }

        public int Station1Exposure
        {
            get => _station1Exposure;
            set => SetProperty(ref _station1Exposure, value);
        }

        public double Station1Gain
        {
            get => _station1Gain;
            set => SetProperty(ref _station1Gain, value);
        }

        public string Station1TriggerSource
        {
            get => _station1TriggerSource;
            set => SetProperty(ref _station1TriggerSource, value);
        }

        public string Station1TriggerEdge
        {
            get => _station1TriggerEdge;
            set => SetProperty(ref _station1TriggerEdge, value);
        }

        public BitmapSource Station1Image
        {
            get => _station1Image;
            set => SetProperty(ref _station1Image, value);
        }

        public SolidColorBrush Station1StatusColor
        {
            get => _station1StatusColor;
            set => SetProperty(ref _station1StatusColor, value);
        }

        public CameraInfo Station1SelectedCamera
        {
            get => _station1SelectedCamera;
            set
            {
                if (SetProperty(ref _station1SelectedCamera, value) && value != null)
                {
                    Station1UserId = value.UserId;
                    Station1FriendlyName = value.FriendlyName;
                }
            }
        }

        #endregion

        #region Station2 Properties

        public string Station2UserId
        {
            get => _station2UserId;
            set => SetProperty(ref _station2UserId, value);
        }

        public string Station2FriendlyName
        {
            get => _station2FriendlyName;
            set => SetProperty(ref _station2FriendlyName, value);
        }

        public string Station2SerialDisplay
        {
            get => _station2SerialDisplay;
            set => SetProperty(ref _station2SerialDisplay, value);
        }

        public string Station2StatusText
        {
            get => _station2StatusText;
            set => SetProperty(ref _station2StatusText, value);
        }

        public int Station2Exposure
        {
            get => _station2Exposure;
            set => SetProperty(ref _station2Exposure, value);
        }

        public double Station2Gain
        {
            get => _station2Gain;
            set => SetProperty(ref _station2Gain, value);
        }

        public int Station2LineRate
        {
            get => _station2LineRate;
            set => SetProperty(ref _station2LineRate, value);
        }

        public int Station2LineCount
        {
            get => _station2LineCount;
            set => SetProperty(ref _station2LineCount, value);
        }

        public string Station2TriggerSource
        {
            get => _station2TriggerSource;
            set => SetProperty(ref _station2TriggerSource, value);
        }

        public string Station2TriggerEdge
        {
            get => _station2TriggerEdge;
            set => SetProperty(ref _station2TriggerEdge, value);
        }

        public BitmapSource Station2Image
        {
            get => _station2Image;
            set => SetProperty(ref _station2Image, value);
        }

        public SolidColorBrush Station2StatusColor
        {
            get => _station2StatusColor;
            set => SetProperty(ref _station2StatusColor, value);
        }

        public CameraInfo Station2SelectedCamera
        {
            get => _station2SelectedCamera;
            set
            {
                if (SetProperty(ref _station2SelectedCamera, value) && value != null)
                {
                    Station2UserId = value.UserId;
                    Station2FriendlyName = value.FriendlyName;
                }
            }
        }

        #endregion

        #region Commands

        public ICommand SearchStation1Command { get; }
        public ICommand ConnectStation1Command { get; }
        public ICommand DisconnectStation1Command { get; }
        public ICommand StartGrabStation1Command { get; }
        public ICommand StopGrabStation1Command { get; }
        public ICommand ApplyStation1Command { get; }
        public ICommand SoftTriggerStation1Command { get; }

        public ICommand SearchStation2Command { get; }
        public ICommand ConnectStation2Command { get; }
        public ICommand DisconnectStation2Command { get; }
        public ICommand StartGrabStation2Command { get; }
        public ICommand StopGrabStation2Command { get; }
        public ICommand ApplyStation2Command { get; }
        public ICommand SoftTriggerStation2Command { get; }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public CameraSettingsViewModel()
        {
            Title = "相机设置";

            // Station1 commands
            SearchStation1Command = new RelayCommand(_ => SearchStation1Cameras());
            ConnectStation1Command = new RelayCommand(_ => ConnectStation1(), _ => _camera1 == null || !_camera1.IsConnected);
            DisconnectStation1Command = new RelayCommand(_ => DisconnectStation1(), _ => _camera1 != null && _camera1.IsConnected);
            StartGrabStation1Command = new RelayCommand(_ => StartGrabStation1(), _ => _camera1 != null && _camera1.IsConnected && !_camera1.IsGrabbing);
            StopGrabStation1Command = new RelayCommand(_ => StopGrabStation1(), _ => _camera1 != null && _camera1.IsGrabbing);
            ApplyStation1Command = new RelayCommand(_ => ApplyStation1Params(), _ => _camera1 != null && _camera1.IsConnected);
            SoftTriggerStation1Command = new RelayCommand(_ => _camera1?.SoftTrigger(), _ => _camera1 != null && _camera1.IsGrabbing && Station1TriggerSource == "Software");

            // Station2 commands
            SearchStation2Command = new RelayCommand(_ => SearchStation2Cameras());
            ConnectStation2Command = new RelayCommand(_ => ConnectStation2(), _ => _camera2 == null || !_camera2.IsConnected);
            DisconnectStation2Command = new RelayCommand(_ => DisconnectStation2(), _ => _camera2 != null && _camera2.IsConnected);
            StartGrabStation2Command = new RelayCommand(_ => StartGrabStation2(), _ => _camera2 != null && _camera2.IsConnected && !_camera2.IsGrabbing);
            StopGrabStation2Command = new RelayCommand(_ => StopGrabStation2(), _ => _camera2 != null && _camera2.IsGrabbing);
            ApplyStation2Command = new RelayCommand(_ => ApplyStation2Params(), _ => _camera2 != null && _camera2.IsConnected);
            SoftTriggerStation2Command = new RelayCommand(_ => _camera2?.SoftTrigger(), _ => _camera2 != null && _camera2.IsGrabbing && Station2TriggerSource == "Software");

            SaveCommand = new RelayCommand(_ => Save());
            CloseCommand = new RelayCommand(_ => Close());

            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Instance.Camera;

            // 工位1
            Station1UserId = config.Station1.UserId ?? "";
            Station1Exposure = config.Station1.Exposure;
            Station1Gain = config.Station1.Gain;
            Station1TriggerSource = config.Station1.TriggerSource ?? "Continuous";
            Station1TriggerEdge = ConvertTriggerEdgeToDisplay(config.Station1.TriggerEdge);

            // 工位2
            Station2UserId = config.Station2.UserId ?? "";
            Station2Exposure = config.Station2.Exposure;
            Station2Gain = config.Station2.Gain;
            Station2LineRate = config.Station2.LineRate;
            Station2LineCount = config.Station2.LineCount;
            Station2TriggerSource = config.Station2.TriggerSource ?? "Continuous";
            Station2TriggerEdge = ConvertTriggerEdgeToDisplay(config.Station2.TriggerEdge);
        }

        private string ConvertTriggerEdgeToDisplay(string edge)
        {
            switch (edge)
            {
                case "FallingEdge": return "下降沿";
                case "DoubleEdge": return "双边沿";
                default: return "上升沿";
            }
        }

        private string ConvertTriggerEdgeToConfig(string display)
        {
            switch (display)
            {
                case "下降沿": return "FallingEdge";
                case "双边沿": return "DoubleEdge";
                default: return "RisingEdge";
            }
        }

        #region Station1 Methods

        private void SearchStation1Cameras()
        {
            try
            {
                Station1Cameras.Clear();
                var camera = CameraFactory.CreateAreaCamera();
                var cameras = camera.SearchCameras();
                camera.Dispose();

                foreach (var cam in cameras)
                {
                    Station1Cameras.Add(cam);
                }

                if (cameras.Length == 0)
                {
                    MessageBox.Show("未找到相机", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    FileLogger.Instance.Info($"搜索到 {cameras.Length} 个相机", "CameraSettings");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"搜索相机失败: {ex.Message}", ex, "CameraSettings");
            }
        }

        private void ConnectStation1()
        {
            try
            {
                DisconnectStation1();

                if (string.IsNullOrEmpty(Station1UserId))
                {
                    // 如果选择了相机，使用索引连接
                    if (Station1SelectedCamera != null)
                    {
                        _camera1 = CameraFactory.CreateAreaCamera();
                        _camera1.ImageReceived += OnStation1ImageReceived;
                        _camera1.ConnectionChanged += OnStation1ConnectionChanged;

                        if (_camera1.ConnectByIndex(Station1SelectedCamera.Index))
                        {
                            UpdateStation1Connected();
                        }
                        else
                        {
                            UpdateStation1Failed();
                        }
                    }
                    else
                    {
                        MessageBox.Show("请先搜索并选择相机，或输入UserId", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // 使用UserId连接
                    _camera1 = CameraFactory.CreateAreaCamera();
                    _camera1.ImageReceived += OnStation1ImageReceived;
                    _camera1.ConnectionChanged += OnStation1ConnectionChanged;

                    if (_camera1.Connect(Station1UserId))
                    {
                        UpdateStation1Connected();
                    }
                    else
                    {
                        UpdateStation1Failed();
                    }
                }

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"工位1相机连接失败: {ex.Message}", ex, "CameraSettings");
            }
        }

        private void UpdateStation1Connected()
        {
            Station1StatusText = "已连接";
            Station1StatusColor = new SolidColorBrush(Colors.LimeGreen);
            Station1SerialDisplay = _camera1.SerialNumber;
            Station1FriendlyName = _camera1.FriendlyName;
            Station1UserId = _camera1.UserId;
            FileLogger.Instance.Info($"工位1相机已连接: {_camera1.FriendlyName}", "CameraSettings");
        }

        private void UpdateStation1Failed()
        {
            Station1StatusText = "连接失败";
            Station1StatusColor = new SolidColorBrush(Colors.Red);
        }

        private void DisconnectStation1()
        {
            if (_camera1 != null)
            {
                _camera1.ImageReceived -= OnStation1ImageReceived;
                _camera1.ConnectionChanged -= OnStation1ConnectionChanged;
                _camera1.Dispose();
                _camera1 = null;

                Station1StatusText = "未连接";
                Station1StatusColor = new SolidColorBrush(Colors.Gray);
                Station1SerialDisplay = "--";
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StartGrabStation1()
        {
            if (_camera1 == null || !_camera1.IsConnected)
                return;

            // 应用当前触发源设置
            _camera1.SetTriggerSource(ParseTriggerSource(Station1TriggerSource));
            _camera1.SetTriggerEdge(ParseTriggerEdge(Station1TriggerEdge));

            if (_camera1.StartGrab())
            {
                Station1StatusText = "采集中";
                FileLogger.Instance.Info($"工位1开始采集, 触发源: {Station1TriggerSource}", "CameraSettings");
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void StopGrabStation1()
        {
            if (_camera1 == null)
                return;

            _camera1.StopGrab();
            Station1StatusText = "已连接";
            CommandManager.InvalidateRequerySuggested();
        }

        private void ApplyStation1Params()
        {
            if (_camera1 == null || !_camera1.IsConnected)
                return;

            _camera1.SetExposure(Station1Exposure);
            _camera1.SetGain(Station1Gain);
            _camera1.SetTriggerSource(ParseTriggerSource(Station1TriggerSource));
            _camera1.SetTriggerEdge(ParseTriggerEdge(Station1TriggerEdge));
            FileLogger.Instance.Info($"工位1参数已应用: Exp={Station1Exposure}, Gain={Station1Gain}, TriggerSource={Station1TriggerSource}", "CameraSettings");
        }

        private void OnStation1ImageReceived(object sender, ImageReceivedEventArgs e)
        {
            BeginRunOnUIThread(() =>
            {
                Station1Image = ConvertToBitmapSource(e.Image);
                e.Image.Dispose();
            });
        }

        private void OnStation1ConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            BeginRunOnUIThread(() =>
            {
                if (e.IsConnected)
                {
                    Station1StatusText = "已连接";
                    Station1StatusColor = new SolidColorBrush(Colors.LimeGreen);
                }
                else
                {
                    Station1StatusText = "已断开";
                    Station1StatusColor = new SolidColorBrush(Colors.Gray);
                }
                CommandManager.InvalidateRequerySuggested();
            });
        }

        #endregion

        #region Station2 Methods

        private void SearchStation2Cameras()
        {
            try
            {
                Station2Cameras.Clear();
                var camera = CameraFactory.CreateLineCamera();
                var cameras = camera.SearchCameras();
                camera.Dispose();

                foreach (var cam in cameras)
                {
                    Station2Cameras.Add(cam);
                }

                if (cameras.Length == 0)
                {
                    MessageBox.Show("未找到相机", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    FileLogger.Instance.Info($"搜索到 {cameras.Length} 个相机", "CameraSettings");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"搜索相机失败: {ex.Message}", ex, "CameraSettings");
            }
        }

        private void ConnectStation2()
        {
            try
            {
                DisconnectStation2();

                if (string.IsNullOrEmpty(Station2UserId))
                {
                    if (Station2SelectedCamera != null)
                    {
                        _camera2 = CameraFactory.CreateLineCamera();
                        _camera2.ImageReceived += OnStation2ImageReceived;
                        _camera2.ConnectionChanged += OnStation2ConnectionChanged;

                        if (_camera2.ConnectByIndex(Station2SelectedCamera.Index))
                        {
                            UpdateStation2Connected();
                        }
                        else
                        {
                            UpdateStation2Failed();
                        }
                    }
                    else
                    {
                        MessageBox.Show("请先搜索并选择相机，或输入UserId", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    _camera2 = CameraFactory.CreateLineCamera();
                    _camera2.ImageReceived += OnStation2ImageReceived;
                    _camera2.ConnectionChanged += OnStation2ConnectionChanged;

                    if (_camera2.Connect(Station2UserId))
                    {
                        UpdateStation2Connected();
                    }
                    else
                    {
                        UpdateStation2Failed();
                    }
                }

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"工位2相机连接失败: {ex.Message}", ex, "CameraSettings");
            }
        }

        private void UpdateStation2Connected()
        {
            Station2StatusText = "已连接";
            Station2StatusColor = new SolidColorBrush(Colors.LimeGreen);
            Station2SerialDisplay = _camera2.SerialNumber;
            Station2FriendlyName = _camera2.FriendlyName;
            Station2UserId = _camera2.UserId;
            FileLogger.Instance.Info($"工位2相机已连接: {_camera2.FriendlyName}", "CameraSettings");
        }

        private void UpdateStation2Failed()
        {
            Station2StatusText = "连接失败";
            Station2StatusColor = new SolidColorBrush(Colors.Red);
        }

        private void DisconnectStation2()
        {
            if (_camera2 != null)
            {
                _camera2.ImageReceived -= OnStation2ImageReceived;
                _camera2.ConnectionChanged -= OnStation2ConnectionChanged;
                _camera2.Dispose();
                _camera2 = null;

                Station2StatusText = "未连接";
                Station2StatusColor = new SolidColorBrush(Colors.Gray);
                Station2SerialDisplay = "--";
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StartGrabStation2()
        {
            if (_camera2 == null || !_camera2.IsConnected)
                return;

            _camera2.SetTriggerSource(ParseTriggerSource(Station2TriggerSource));
            _camera2.SetTriggerEdge(ParseTriggerEdge(Station2TriggerEdge));

            if (_camera2.StartGrab())
            {
                Station2StatusText = "采集中";
                FileLogger.Instance.Info($"工位2开始采集, 触发源: {Station2TriggerSource}", "CameraSettings");
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void StopGrabStation2()
        {
            if (_camera2 == null)
                return;

            _camera2.StopGrab();
            Station2StatusText = "已连接";
            CommandManager.InvalidateRequerySuggested();
        }

        private void ApplyStation2Params()
        {
            if (_camera2 == null || !_camera2.IsConnected)
                return;

            _camera2.SetExposure(Station2Exposure);
            _camera2.SetGain(Station2Gain);
            _camera2.SetTriggerSource(ParseTriggerSource(Station2TriggerSource));
            _camera2.SetTriggerEdge(ParseTriggerEdge(Station2TriggerEdge));
            _camera2.SetLineRate(Station2LineRate);
            _camera2.SetLineCount(Station2LineCount);

            FileLogger.Instance.Info($"工位2参数已应用: Exp={Station2Exposure}, Gain={Station2Gain}, TriggerSource={Station2TriggerSource}", "CameraSettings");
        }

        private void OnStation2ImageReceived(object sender, ImageReceivedEventArgs e)
        {
            BeginRunOnUIThread(() =>
            {
                Station2Image = ConvertToBitmapSource(e.Image);
                e.Image.Dispose();
            });
        }

        private void OnStation2ConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            BeginRunOnUIThread(() =>
            {
                if (e.IsConnected)
                {
                    Station2StatusText = "已连接";
                    Station2StatusColor = new SolidColorBrush(Colors.LimeGreen);
                }
                else
                {
                    Station2StatusText = "已断开";
                    Station2StatusColor = new SolidColorBrush(Colors.Gray);
                }
                CommandManager.InvalidateRequerySuggested();
            });
        }

        #endregion

        private void Save()
        {
            try
            {
                var config = ConfigManager.Instance.Camera;

                config.Station1.UserId = Station1UserId;
                config.Station1.Exposure = Station1Exposure;
                config.Station1.Gain = Station1Gain;
                config.Station1.TriggerSource = Station1TriggerSource;
                config.Station1.TriggerEdge = ConvertTriggerEdgeToConfig(Station1TriggerEdge);

                config.Station2.UserId = Station2UserId;
                config.Station2.Exposure = Station2Exposure;
                config.Station2.Gain = Station2Gain;
                config.Station2.LineRate = Station2LineRate;
                config.Station2.LineCount = Station2LineCount;
                config.Station2.TriggerSource = Station2TriggerSource;
                config.Station2.TriggerEdge = ConvertTriggerEdgeToConfig(Station2TriggerEdge);

                ConfigManager.Instance.SaveCameraConfig();

                FileLogger.Instance.Info("相机配置已保存", "CameraSettings");
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"保存相机配置失败: {ex.Message}", ex, "CameraSettings");
            }
        }

        private void Close()
        {
            RequestClose?.Invoke(this, true);
        }

        private TriggerSource ParseTriggerSource(string source)
        {
            switch (source)
            {
                case "Software": return TriggerSource.Software;
                case "Line0": return TriggerSource.Line0;
                case "Line1": return TriggerSource.Line1;
                case "Line2": return TriggerSource.Line2;
                case "Line3": return TriggerSource.Line3;
                default: return TriggerSource.Continuous;
            }
        }

        private TriggerEdge ParseTriggerEdge(string edge)
        {
            switch (edge)
            {
                case "下降沿": return TriggerEdge.FallingEdge;
                case "双边沿": return TriggerEdge.DoubleEdge;
                default: return TriggerEdge.RisingEdge;
            }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            try
            {
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                PixelFormat format;
                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        format = PixelFormats.Bgr24;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                        format = PixelFormats.Bgra32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                        format = PixelFormats.Gray8;
                        break;
                    default:
                        format = PixelFormats.Bgr24;
                        break;
                }

                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width,
                    bitmapData.Height,
                    bitmap.HorizontalResolution,
                    bitmap.VerticalResolution,
                    format,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);

                bitmap.UnlockBits(bitmapData);
                bitmapSource.Freeze();

                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }

        public override void Cleanup()
        {
            DisconnectStation1();
            DisconnectStation2();
            base.Cleanup();
        }
    }
}
