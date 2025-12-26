using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Plc;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// PLC设置视图模型 - 使用 PlcAddressViewModel 简化重复代码
    /// </summary>
    public class PlcSettingsViewModel : ViewModelBase
    {
        private IPlcCommunication _plc;
        private bool _isConnected;

        private string _ipAddress;
        private int _port;
        private int _timeout;

        #region 地址集合

        /// <summary>
        /// 输出地址集合
        /// </summary>
        public ObservableCollection<PlcAddressViewModel> OutputAddresses { get; } = new ObservableCollection<PlcAddressViewModel>();

        /// <summary>
        /// 输入地址集合
        /// </summary>
        public ObservableCollection<PlcAddressViewModel> InputAddresses { get; } = new ObservableCollection<PlcAddressViewModel>();

        /// <summary>
        /// 测试触发地址集合
        /// </summary>
        public ObservableCollection<PlcAddressViewModel> TestTriggerAddresses { get; } = new ObservableCollection<PlcAddressViewModel>();

        /// <summary>
        /// 支持的数据类型
        /// </summary>
        public List<string> DataTypes { get; } = new List<string>
        {
            "BOOL", "INT", "UINT", "DINT", "UDINT", "LINT", "ULINT", "REAL", "LREAL"
        };

        #endregion

        public event EventHandler<bool> RequestClose;

        #region Properties

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RefreshAddressCommands();
                }
            }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ReadAllCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public PlcSettingsViewModel()
        {
            Title = "PLC设置";

            ConnectCommand = CommandFactory.Create(async () => await ConnectAsync(), () => !IsConnected);
            DisconnectCommand = CommandFactory.Create(Disconnect, () => IsConnected);
            ReadAllCommand = CommandFactory.Create(async () => await ReadAllAsync(), () => IsConnected);
            SaveCommand = CommandFactory.Create(Save);
            CancelCommand = CommandFactory.Create(Cancel);

            InitializeAddresses();
            LoadConfig();
        }

        private void InitializeAddresses()
        {
            // 输出地址
            OutputAddresses.Add(new PlcAddressViewModel("输出值（瓶身旋转）", () => _plc, RefreshAddressCommands));
            OutputAddresses.Add(new PlcAddressViewModel("定位角度", () => _plc, RefreshAddressCommands));
            OutputAddresses.Add(new PlcAddressViewModel("检测结果", () => _plc, RefreshAddressCommands));

            // 输入地址
            InputAddresses.Add(new PlcAddressViewModel("瓶身旋转角度", () => _plc, RefreshAddressCommands));

            // 测试触发地址
            TestTriggerAddresses.Add(new PlcAddressViewModel("工位1触发(面阵)", () => _plc, RefreshAddressCommands));
            TestTriggerAddresses.Add(new PlcAddressViewModel("工位2触发(线扫)", () => _plc, RefreshAddressCommands));
        }

        private void RefreshAddressCommands()
        {
            foreach (var addr in OutputAddresses)
            {
                addr.RefreshCommands();
            }
            foreach (var addr in InputAddresses)
            {
                addr.RefreshCommands();
            }
            foreach (var addr in TestTriggerAddresses)
            {
                addr.RefreshCommands();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Instance.Plc;

            // 连接配置
            IpAddress = config.Connection.IP;
            Port = config.Connection.Port;
            Timeout = config.Connection.Timeout;

            // 输出地址
            if (OutputAddresses.Count >= 3)
            {
                OutputAddresses[0].Address = config.OutputAddresses.OutputValue.Address;
                OutputAddresses[0].DataType = config.OutputAddresses.OutputValue.DataType;

                OutputAddresses[1].Address = config.OutputAddresses.RotationAngle.Address;
                OutputAddresses[1].DataType = config.OutputAddresses.RotationAngle.DataType;

                OutputAddresses[2].Address = config.OutputAddresses.Result.Address;
                OutputAddresses[2].DataType = config.OutputAddresses.Result.DataType;
            }

            // 输入地址
            if (InputAddresses.Count >= 1)
            {
                InputAddresses[0].Address = config.InputAddresses.RotationAngle.Address;
                InputAddresses[0].DataType = config.InputAddresses.RotationAngle.DataType;
            }

            // 测试触发地址
            if (TestTriggerAddresses.Count >= 2)
            {
                TestTriggerAddresses[0].Address = config.TestTrigger.Station1Trigger.Address;
                TestTriggerAddresses[0].DataType = config.TestTrigger.Station1Trigger.DataType;

                TestTriggerAddresses[1].Address = config.TestTrigger.Station2Trigger.Address;
                TestTriggerAddresses[1].DataType = config.TestTrigger.Station2Trigger.DataType;
            }
        }

        private async Task ConnectAsync()
        {
            try
            {
                _plc = new OmronFinsCommunication(IpAddress, Port);
                var connected = await _plc.ConnectAsync();
                if (connected)
                {
                    IsConnected = true;
                    this.LogInfo($"PLC已连接: {IpAddress}:{Port}");
                }
                else
                {
                    MessageBox.Show("PLC连接失败", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                this.LogError("PLC连接失败", ex);
            }
        }

        private void Disconnect()
        {
            try
            {
                _plc?.Dispose();
                _plc = null;
                IsConnected = false;

                // 清空所有当前值
                foreach (var addr in OutputAddresses.Concat(InputAddresses).Concat(TestTriggerAddresses))
                {
                    addr.CurrentValue = "--";
                }

                this.LogInfo("PLC已断开");
            }
            catch (Exception ex)
            {
                this.LogError("PLC断开失败", ex);
            }
        }

        private async Task ReadAllAsync()
        {
            foreach (var addr in OutputAddresses.Concat(InputAddresses).Concat(TestTriggerAddresses))
            {
                await addr.ReadAsync();
            }
        }

        private void Save()
        {
            try
            {
                var config = ConfigManager.Instance.Plc;

                // 更新连接配置
                config.Connection.IP = IpAddress;
                config.Connection.Port = Port;
                config.Connection.Timeout = Timeout;

                // 更新输出地址
                if (OutputAddresses.Count >= 3)
                {
                    config.OutputAddresses.OutputValue.Address = OutputAddresses[0].Address;
                    config.OutputAddresses.OutputValue.DataType = OutputAddresses[0].DataType;
                    config.OutputAddresses.OutputValue.Description = "瓶身旋转地址 (1=旋转, 0=不旋转)";

                    config.OutputAddresses.RotationAngle.Address = OutputAddresses[1].Address;
                    config.OutputAddresses.RotationAngle.DataType = OutputAddresses[1].DataType;
                    config.OutputAddresses.RotationAngle.Description = "定位角度地址";

                    config.OutputAddresses.Result.Address = OutputAddresses[2].Address;
                    config.OutputAddresses.Result.DataType = OutputAddresses[2].DataType;
                    config.OutputAddresses.Result.Description = "产品合格地址 (2=合格, 3=不合格)";
                }

                // 更新输入地址
                if (InputAddresses.Count >= 1)
                {
                    config.InputAddresses.RotationAngle.Address = InputAddresses[0].Address;
                    config.InputAddresses.RotationAngle.DataType = InputAddresses[0].DataType;
                    config.InputAddresses.RotationAngle.Description = "瓶身旋转角度（启动时读取写入VisionMaster）";
                }

                // 更新测试触发地址
                if (TestTriggerAddresses.Count >= 2)
                {
                    config.TestTrigger.Station1Trigger.Address = TestTriggerAddresses[0].Address;
                    config.TestTrigger.Station1Trigger.DataType = TestTriggerAddresses[0].DataType;
                    config.TestTrigger.Station1Trigger.Description = "工位1(面阵)测试触发";

                    config.TestTrigger.Station2Trigger.Address = TestTriggerAddresses[1].Address;
                    config.TestTrigger.Station2Trigger.DataType = TestTriggerAddresses[1].DataType;
                    config.TestTrigger.Station2Trigger.Description = "工位2(线扫)测试触发";
                }

                // 保存到文件
                ConfigManager.Instance.SavePlcConfig();

                this.LogInfo("PLC配置已保存");
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                this.LogError("保存PLC配置失败", ex);
            }
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }

        public override void Cleanup()
        {
            Disconnect();
            base.Cleanup();
        }
    }
}
