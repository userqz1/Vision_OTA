using System;
using System.Threading.Tasks;
using System.Windows.Input;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Plc;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// PLC地址视图模型 - 封装单个PLC地址的读写操作
    /// </summary>
    public class PlcAddressViewModel : ViewModelBase
    {
        private readonly Func<IPlcCommunication> _getPlc;
        private readonly Action _onAddressChanged;

        private string _name;
        private string _address;
        private string _dataType = "REAL";
        private string _currentValue = "--";
        private string _writeValue = "0";
        private bool _isReading;
        private bool _isWriting;

        #region 属性

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// PLC地址
        /// </summary>
        public string Address
        {
            get => _address;
            set
            {
                if (SetProperty(ref _address, value))
                {
                    _onAddressChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType
        {
            get => _dataType;
            set
            {
                if (SetProperty(ref _dataType, value))
                {
                    _onAddressChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 当前读取值
        /// </summary>
        public string CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        /// <summary>
        /// 待写入值
        /// </summary>
        public string WriteValue
        {
            get => _writeValue;
            set => SetProperty(ref _writeValue, value);
        }

        /// <summary>
        /// 是否正在读取
        /// </summary>
        public bool IsReading
        {
            get => _isReading;
            set => SetProperty(ref _isReading, value);
        }

        /// <summary>
        /// 是否正在写入
        /// </summary>
        public bool IsWriting
        {
            get => _isWriting;
            set => SetProperty(ref _isWriting, value);
        }

        #endregion

        #region 命令

        public ICommand ReadCommand { get; }
        public ICommand WriteCommand { get; }

        #endregion

        /// <summary>
        /// 支持的数据类型列表
        /// </summary>
        public static string[] DataTypes { get; } = { "BOOL", "INT", "UINT", "DINT", "UDINT", "REAL", "LREAL" };

        public PlcAddressViewModel(string name, Func<IPlcCommunication> getPlc, Action onAddressChanged = null)
        {
            _name = name;
            _getPlc = getPlc;
            _onAddressChanged = onAddressChanged;

            ReadCommand = new RelayCommand(async _ => await ReadAsync(), _ => CanRead());
            WriteCommand = new RelayCommand(async _ => await WriteAsync(), _ => CanWrite());
        }

        private bool CanRead()
        {
            var plc = _getPlc?.Invoke();
            return plc != null && plc.IsConnected && !string.IsNullOrEmpty(Address) && !IsReading;
        }

        private bool CanWrite()
        {
            var plc = _getPlc?.Invoke();
            return plc != null && plc.IsConnected && !string.IsNullOrEmpty(Address) && !IsWriting;
        }

        /// <summary>
        /// 读取PLC值
        /// </summary>
        public async Task ReadAsync()
        {
            var plc = _getPlc?.Invoke();
            if (plc == null || !plc.IsConnected || string.IsNullOrEmpty(Address))
            {
                CurrentValue = "--";
                return;
            }

            try
            {
                IsReading = true;
                CurrentValue = await ReadByTypeAsync(plc);
                FileLogger.Instance.Debug($"读取PLC地址 {Address}: {CurrentValue}", "PLC");
            }
            catch (Exception ex)
            {
                CurrentValue = "错误";
                FileLogger.Instance.Error($"读取PLC地址 {Address} 失败: {ex.Message}", ex, "PLC");
            }
            finally
            {
                IsReading = false;
            }
        }

        /// <summary>
        /// 写入PLC值
        /// </summary>
        public async Task WriteAsync()
        {
            var plc = _getPlc?.Invoke();
            if (plc == null || !plc.IsConnected || string.IsNullOrEmpty(Address))
                return;

            try
            {
                IsWriting = true;
                var success = await WriteByTypeAsync(plc);
                if (success)
                {
                    FileLogger.Instance.Info($"写入PLC地址 {Address}: {WriteValue}", "PLC");
                }
                else
                {
                    FileLogger.Instance.Warning($"写入PLC地址 {Address} 失败", "PLC");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"写入PLC地址 {Address} 失败: {ex.Message}", ex, "PLC");
            }
            finally
            {
                IsWriting = false;
            }
        }

        private async Task<string> ReadByTypeAsync(IPlcCommunication plc)
        {
            switch (DataType?.ToUpper())
            {
                case "BOOL":
                    var boolVal = await plc.ReadBitAsync(Address);
                    return boolVal ? "1" : "0";

                case "INT":
                    var intVal = await plc.ReadWordAsync(Address);
                    return intVal.ToString();

                case "UINT":
                    var uintVal = await plc.ReadUIntAsync(Address);
                    return uintVal.ToString();

                case "DINT":
                    var dintVal = await plc.ReadDWordAsync(Address);
                    return dintVal.ToString();

                case "UDINT":
                    var udintVal = await plc.ReadUDIntAsync(Address);
                    return udintVal.ToString();

                case "REAL":
                    var realVal = await plc.ReadFloatAsync(Address);
                    return realVal.ToString("F2");

                case "LREAL":
                    var lrealVal = await plc.ReadLRealAsync(Address);
                    return lrealVal.ToString("F4");

                default:
                    var defaultVal = await plc.ReadFloatAsync(Address);
                    return defaultVal.ToString("F2");
            }
        }

        private async Task<bool> WriteByTypeAsync(IPlcCommunication plc)
        {
            switch (DataType?.ToUpper())
            {
                case "BOOL":
                    var boolVal = WriteValue == "1" || WriteValue.ToLower() == "true";
                    return await plc.WriteBitAsync(Address, boolVal);

                case "INT":
                    if (short.TryParse(WriteValue, out var intVal))
                        return await plc.WriteWordAsync(Address, intVal);
                    return false;

                case "UINT":
                    if (ushort.TryParse(WriteValue, out var uintVal))
                        return await plc.WriteUIntAsync(Address, uintVal);
                    return false;

                case "DINT":
                    if (int.TryParse(WriteValue, out var dintVal))
                        return await plc.WriteDWordAsync(Address, dintVal);
                    return false;

                case "UDINT":
                    if (uint.TryParse(WriteValue, out var udintVal))
                        return await plc.WriteUDIntAsync(Address, udintVal);
                    return false;

                case "REAL":
                    if (float.TryParse(WriteValue, out var realVal))
                        return await plc.WriteFloatAsync(Address, realVal);
                    return false;

                case "LREAL":
                    if (double.TryParse(WriteValue, out var lrealVal))
                        return await plc.WriteLRealAsync(Address, lrealVal);
                    return false;

                default:
                    if (float.TryParse(WriteValue, out var defaultVal))
                        return await plc.WriteFloatAsync(Address, defaultVal);
                    return false;
            }
        }

        /// <summary>
        /// 刷新命令状态
        /// </summary>
        public void RefreshCommands()
        {
            (ReadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (WriteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
