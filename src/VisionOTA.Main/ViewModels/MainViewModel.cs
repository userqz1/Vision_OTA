using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VisionOTA.Common.Constants;
using VisionOTA.Common.Events;
using VisionOTA.Common.Mvvm;
using VisionOTA.Core.Interfaces;
using VisionOTA.Core.Models;
using VisionOTA.Core.Services;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Permission;

namespace VisionOTA.Main.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IInspectionService _inspectionService;
        private readonly IStatisticsService _statisticsService;
        private readonly DispatcherTimer _timer;

        private string _systemStatus = "空闲";
        private SolidColorBrush _systemStatusColor = new SolidColorBrush(Colors.Gray);
        private string _currentUser = "未登录";
        private string _statusMessage = "系统就绪";
        private DateTime _currentTime;

        // 登录面板
        private bool _isLoginPanelVisible;
        private string _selectedUserType;
        private string _loginPassword = string.Empty;
        private string _loginError = string.Empty;
        private bool _isLoggedIn;

        // 用户类型列表
        public string[] UserTypes { get; } = { "操作员", "工程师", "管理员" };

        // 工位1
        private BitmapSource _station1Image;
        private SolidColorBrush _station1StatusColor = new SolidColorBrush(Colors.Gray);
        private int _station1Total;
        private int _station1Ok;
        private int _station1Ng;
        private double _station1Rate;

        // 工位2
        private BitmapSource _station2Image;
        private SolidColorBrush _station2StatusColor = new SolidColorBrush(Colors.Gray);
        private int _station2Total;
        private int _station2Ok;
        private int _station2Ng;
        private double _station2Rate;

        // 最新结果
        private string _lastResultText = "--";
        private SolidColorBrush _lastResultBackground = new SolidColorBrush(Colors.Gray);
        private double _lastAngle;

        // 设备状态
        private SolidColorBrush _plcStatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _camera1StatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _camera2StatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _visionStatusColor = new SolidColorBrush(Colors.Gray);

        #region Properties

        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(ref _systemStatus, value);
        }

        public SolidColorBrush SystemStatusColor
        {
            get => _systemStatusColor;
            set => SetProperty(ref _systemStatusColor, value);
        }

        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public bool IsAdmin => PermissionService.Instance.HasPermission(PermissionLevel.Administrator);

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (SetProperty(ref _isLoggedIn, value))
                {
                    OnPropertyChanged(nameof(IsAdmin));
                    OnPropertyChanged(nameof(CanOperate));
                }
            }
        }

        public bool CanOperate => IsLoggedIn && PermissionService.Instance.HasPermission(PermissionLevel.Operator);

        public bool IsLoginPanelVisible
        {
            get => _isLoginPanelVisible;
            set => SetProperty(ref _isLoginPanelVisible, value);
        }

        public string SelectedUserType
        {
            get => _selectedUserType;
            set => SetProperty(ref _selectedUserType, value);
        }

        public string LoginPassword
        {
            get => _loginPassword;
            set => SetProperty(ref _loginPassword, value);
        }

        public string LoginError
        {
            get => _loginError;
            set => SetProperty(ref _loginError, value);
        }

        // 工位1
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

        public int Station1Total
        {
            get => _station1Total;
            set => SetProperty(ref _station1Total, value);
        }

        public int Station1Ok
        {
            get => _station1Ok;
            set => SetProperty(ref _station1Ok, value);
        }

        public int Station1Ng
        {
            get => _station1Ng;
            set => SetProperty(ref _station1Ng, value);
        }

        public double Station1Rate
        {
            get => _station1Rate;
            set => SetProperty(ref _station1Rate, value);
        }

        // 工位2
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

        public int Station2Total
        {
            get => _station2Total;
            set => SetProperty(ref _station2Total, value);
        }

        public int Station2Ok
        {
            get => _station2Ok;
            set => SetProperty(ref _station2Ok, value);
        }

        public int Station2Ng
        {
            get => _station2Ng;
            set => SetProperty(ref _station2Ng, value);
        }

        public double Station2Rate
        {
            get => _station2Rate;
            set => SetProperty(ref _station2Rate, value);
        }

        // 最新结果
        public string LastResultText
        {
            get => _lastResultText;
            set => SetProperty(ref _lastResultText, value);
        }

        public SolidColorBrush LastResultBackground
        {
            get => _lastResultBackground;
            set => SetProperty(ref _lastResultBackground, value);
        }

        public double LastAngle
        {
            get => _lastAngle;
            set => SetProperty(ref _lastAngle, value);
        }

        // 设备状态
        public SolidColorBrush PlcStatusColor
        {
            get => _plcStatusColor;
            set => SetProperty(ref _plcStatusColor, value);
        }

        public SolidColorBrush Camera1StatusColor
        {
            get => _camera1StatusColor;
            set => SetProperty(ref _camera1StatusColor, value);
        }

        public SolidColorBrush Camera2StatusColor
        {
            get => _camera2StatusColor;
            set => SetProperty(ref _camera2StatusColor, value);
        }

        public SolidColorBrush VisionStatusColor
        {
            get => _visionStatusColor;
            set => SetProperty(ref _visionStatusColor, value);
        }

        #endregion

        #region Commands

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ShowLoginCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand CancelLoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenCameraSettingsCommand { get; }
        public ICommand OpenPlcSettingsCommand { get; }
        public ICommand OpenVisionSettingsCommand { get; }
        public ICommand OpenStatisticsCommand { get; }
        public ICommand OpenLogCommand { get; }
        public ICommand OpenUserManagementCommand { get; }

        #endregion

        public MainViewModel()
        {
            _statisticsService = new StatisticsService();
            _inspectionService = new InspectionService(_statisticsService);

            // 初始化命令
            StartCommand = new RelayCommand(async _ => await StartInspection(), _ => CanOperate && _inspectionService.CurrentState != SystemState.Running);
            StopCommand = new RelayCommand(async _ => await StopInspection(), _ => CanOperate && _inspectionService.CurrentState == SystemState.Running);
            ShowLoginCommand = new RelayCommand(_ => ShowLoginPanel(), _ => !IsLoggedIn);
            LoginCommand = new RelayCommand(_ => DoLogin());
            CancelLoginCommand = new RelayCommand(_ => CancelLogin());
            LogoutCommand = new RelayCommand(_ => Logout(), _ => IsLoggedIn);
            OpenCameraSettingsCommand = new RelayCommand(_ => OpenCameraSettings(), _ => CanOperate);
            OpenPlcSettingsCommand = new RelayCommand(_ => OpenPlcSettings(), _ => CanOperate);
            OpenVisionSettingsCommand = new RelayCommand(_ => OpenVisionSettings(), _ => CanOperate);
            OpenStatisticsCommand = new RelayCommand(_ => OpenStatistics());
            OpenLogCommand = new RelayCommand(_ => OpenLog());
            OpenUserManagementCommand = new RelayCommand(_ => OpenUserManagement(), _ => IsAdmin);

            // 订阅事件
            _inspectionService.InspectionCompleted += OnInspectionCompleted;
            _inspectionService.StateChanged += OnStateChanged;
            _inspectionService.ErrorOccurred += OnErrorOccurred;

            EventAggregator.Instance.Subscribe<ConnectionChangedEvent>(OnConnectionChanged);

            // 初始化定时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now;
                UpdateStatistics();
            };
            _timer.Start();

            // 设置当前用户状态
            UpdateLoginState();
        }

        private void ShowLoginPanel()
        {
            SelectedUserType = UserTypes[0]; // 默认选中操作员
            LoginPassword = string.Empty;
            LoginError = string.Empty;
            IsLoginPanelVisible = true;
        }

        private void DoLogin()
        {
            if (string.IsNullOrWhiteSpace(SelectedUserType))
            {
                LoginError = "请选择用户类型";
                return;
            }

            if (string.IsNullOrWhiteSpace(LoginPassword))
            {
                LoginError = "请输入密码";
                return;
            }

            var success = PermissionService.Instance.Login(SelectedUserType, LoginPassword);
            if (success)
            {
                IsLoginPanelVisible = false;
                UpdateLoginState();
                StatusMessage = $"{CurrentUser} 已登录";
                FileLogger.Instance.Info($"{CurrentUser} 登录成功", "Auth");
            }
            else
            {
                LoginError = "密码错误";
                LoginPassword = string.Empty; // 清空密码，触发 View 清空 PasswordBox
                FileLogger.Instance.Warning($"{SelectedUserType} 登录失败", "Auth");
            }
        }

        private void CancelLogin()
        {
            IsLoginPanelVisible = false;
            SelectedUserType = null;
            LoginPassword = string.Empty;
            LoginError = string.Empty;
        }

        private void UpdateLoginState()
        {
            var user = PermissionService.Instance.CurrentUser;
            IsLoggedIn = user != null;
            CurrentUser = user?.Username ?? "未登录";

            // 刷新所有命令的CanExecute状态
            CommandManager.InvalidateRequerySuggested();
        }

        public async Task InitializeAsync()
        {
            StatusMessage = "正在初始化...";

            // 设备状态保持灰色直到收到实际连接事件
            PlcStatusColor = new SolidColorBrush(Colors.Gray);
            Camera1StatusColor = new SolidColorBrush(Colors.Gray);
            Camera2StatusColor = new SolidColorBrush(Colors.Gray);
            VisionStatusColor = new SolidColorBrush(Colors.Gray);

            var success = await _inspectionService.InitializeAsync();

            if (success)
            {
                StatusMessage = "初始化完成";
                // 设备状态由ConnectionChangedEvent事件更新，不在此处硬编码
            }
            else
            {
                StatusMessage = "初始化失败，部分功能不可用";
            }

            UpdateStatistics();
        }

        private async Task StartInspection()
        {
            var success = await _inspectionService.StartAsync();
            if (success)
            {
                StatusMessage = "检测已启动";
            }
        }

        private async Task StopInspection()
        {
            await _inspectionService.StopAsync();
            StatusMessage = "检测已停止";
        }

        private void Logout()
        {
            var username = CurrentUser;
            PermissionService.Instance.Logout();
            UpdateLoginState();
            StatusMessage = $"用户 {username} 已登出";
            FileLogger.Instance.Info($"用户 {username} 登出", "Auth");
        }

        private void OpenCameraSettings()
        {
            var window = new Views.CameraSettingsWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenPlcSettings()
        {
            var window = new Views.PlcSettingsWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenVisionSettings()
        {
            var window = new Views.VisionSettingsWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenStatistics()
        {
            StatusMessage = "统计报表 - 功能开发中";
        }

        private void OpenLog()
        {
            var window = new Views.LogWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenUserManagement()
        {
            var window = new Views.UserManagementWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OnInspectionCompleted(object sender, InspectionCompletedEventArgs e)
        {
            RunOnUIThread(() =>
            {
                var result = e.Result;

                // 更新最新结果
                LastResultText = result.IsOk ? "OK" : "NG";
                LastResultBackground = result.IsOk
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
                    : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                LastAngle = result.Angle;

                // 更新工位状态
                if (result.StationId == 1)
                {
                    Station1StatusColor = result.IsOk
                        ? new SolidColorBrush(Colors.LimeGreen)
                        : new SolidColorBrush(Colors.Red);

                    if (result.ResultImage != null)
                    {
                        Station1Image = ConvertToBitmapSource(result.ResultImage);
                    }
                }
                else if (result.StationId == 2)
                {
                    Station2StatusColor = result.IsOk
                        ? new SolidColorBrush(Colors.LimeGreen)
                        : new SolidColorBrush(Colors.Red);

                    if (result.ResultImage != null)
                    {
                        Station2Image = ConvertToBitmapSource(result.ResultImage);
                    }
                }

                UpdateStatistics();
            });
        }

        private void OnStateChanged(object sender, SystemState state)
        {
            RunOnUIThread(() =>
            {
                switch (state)
                {
                    case SystemState.Idle:
                        SystemStatus = "空闲";
                        SystemStatusColor = new SolidColorBrush(Colors.Gray);
                        break;
                    case SystemState.Running:
                        SystemStatus = "运行中";
                        SystemStatusColor = new SolidColorBrush(Colors.LimeGreen);
                        break;
                    case SystemState.Paused:
                        SystemStatus = "已暂停";
                        SystemStatusColor = new SolidColorBrush(Colors.Orange);
                        break;
                    case SystemState.Error:
                        SystemStatus = "错误";
                        SystemStatusColor = new SolidColorBrush(Colors.Red);
                        break;
                }
            });
        }

        private void OnErrorOccurred(object sender, string error)
        {
            RunOnUIThread(() =>
            {
                StatusMessage = error;
                FileLogger.Instance.Error(error, "Inspection");
            });
        }

        private void OnConnectionChanged(ConnectionChangedEvent e)
        {
            RunOnUIThread(() =>
            {
                var color = e.IsConnected
                    ? new SolidColorBrush(Colors.LimeGreen)
                    : new SolidColorBrush(Colors.Red);

                switch (e.DeviceType)
                {
                    case "Camera1":
                        Camera1StatusColor = color;
                        break;
                    case "Camera2":
                        Camera2StatusColor = color;
                        break;
                    case "PLC":
                        PlcStatusColor = color;
                        break;
                    case "Vision":
                        VisionStatusColor = color;
                        break;
                }
            });
        }

        private void UpdateStatistics()
        {
            var stat1 = _statisticsService.GetStationStatistics(1);
            var stat2 = _statisticsService.GetStationStatistics(2);

            if (stat1 != null)
            {
                Station1Total = stat1.TotalCount;
                Station1Ok = stat1.OkCount;
                Station1Ng = stat1.NgCount;
                Station1Rate = stat1.OkRate;
            }

            if (stat2 != null)
            {
                Station2Total = stat2.TotalCount;
                Station2Ok = stat2.OkCount;
                Station2Ng = stat2.NgCount;
                Station2Rate = stat2.OkRate;
            }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgr24,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            bitmapSource.Freeze();

            return bitmapSource;
        }

        public override void Cleanup()
        {
            _timer.Stop();
            _inspectionService?.Dispose();
            (_statisticsService as IDisposable)?.Dispose();
            base.Cleanup();
        }
    }
}
