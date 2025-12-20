using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using VM.Core;
using VM.PlatformSDKCS;
using GlobalVariableModuleCs;
using VisionOTA.Common.Events;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Hardware.Plc;

namespace VisionOTA.Main.Views
{
    /// <summary>
    /// VisionMaster算法设置窗口
    /// </summary>
    public partial class VisionMasterSettingsWindow : Window
    {
        private const string DEFAULT_SOLUTION_PATH = @"D:\Vision_OTA\杭州腾隆化妆瓶算法流程.sol";
        private const string STATION1_NAME = "瓶底工位";
        private const string STATION2_NAME = "瓶身工位";

        private VmProcedure _station1Procedure;
        private VmProcedure _station2Procedure;

        private bool _isSolutionLoaded = false;

        public VisionMasterSettingsWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查方案是否已加载（软件初始化时已加载）
                if (VmSolution.Instance != null)
                {
                    // 使用已加载的方案
                    UseExistingSolution();
                }
                else if (System.IO.File.Exists(DEFAULT_SOLUTION_PATH))
                {
                    // 方案未加载，尝试加载
                    await LoadSolutionAsync(DEFAULT_SOLUTION_PATH);
                }
                else
                {
                    UpdateStatus(false, "请加载VisionMaster方案文件");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"初始化失败: {ex.Message}", true);
                FileLogger.Instance.Error($"VisionMaster设置窗口初始化失败: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 使用已加载的方案（不重新加载）
        /// </summary>
        private void UseExistingSolution()
        {
            try
            {
                _isSolutionLoaded = true;
                txtSolutionPath.Text = System.IO.Path.GetFileName(DEFAULT_SOLUTION_PATH);

                // 获取流程
                _station1Procedure = VmSolution.Instance[STATION1_NAME] as VmProcedure;
                _station2Procedure = VmSolution.Instance[STATION2_NAME] as VmProcedure;

                bool station1Ok = _station1Procedure != null;
                bool station2Ok = _station2Procedure != null;

                if (station1Ok && station2Ok)
                {
                    UpdateStatus(true, "方案已就绪");
                    ShowMessage("使用已加载的方案", false);
                }
                else
                {
                    UpdateStatus(true, "方案已就绪");
                    ShowMessage($"流程状态: 瓶底={station1Ok}, 瓶身={station2Ok}", false);
                }

                FileLogger.Instance.Info("算法设置窗口使用已加载的方案", "VisionMaster");

                // 发布Vision状态事件（确保主界面状态灯正确）
                EventAggregator.Instance.Publish(new ConnectionChangedEvent
                {
                    DeviceType = "Vision",
                    DeviceName = "VisionMaster",
                    IsConnected = true
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"获取已加载方案失败: {ex.Message}", "VisionMaster");
                // 如果获取失败，尝试重新加载
                if (System.IO.File.Exists(DEFAULT_SOLUTION_PATH))
                {
                    _ = LoadSolutionAsync(DEFAULT_SOLUTION_PATH);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 注意：不在这里关闭方案，因为方案可能被其他地方使用
        }

        /// <summary>
        /// 加载方案按钮点击
        /// </summary>
        private async void BtnLoadSolution_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择VisionMaster方案文件",
                Filter = "VisionMaster方案|*.sol|所有文件|*.*",
                InitialDirectory = System.IO.Path.GetDirectoryName(DEFAULT_SOLUTION_PATH)
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadSolutionAsync(dialog.FileName);
            }
        }

        /// <summary>
        /// 异步加载方案
        /// </summary>
        private async System.Threading.Tasks.Task LoadSolutionAsync(string solutionPath)
        {
            try
            {
                ShowMessage("正在加载方案...", false);
                btnLoadSolution.IsEnabled = false;
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // 使用超时机制加载方案
                var loadTask = System.Threading.Tasks.Task.Run(() =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // 如果已加载方案，先关闭
                    try
                    {
                        if (VmSolution.Instance != null)
                        {
                            FileLogger.Instance.Info("正在关闭旧方案...", "VisionMaster");
                            VmSolution.Instance.CloseSolution();
                            FileLogger.Instance.Info($"旧方案已关闭，耗时: {sw.ElapsedMilliseconds}ms", "VisionMaster");
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"关闭旧方案时出错: {ex.Message}", "VisionMaster");
                    }

                    // 加载新方案
                    sw.Restart();
                    FileLogger.Instance.Info($"开始加载新方案: {solutionPath}", "VisionMaster");
                    VmSolution.Load(solutionPath);
                    FileLogger.Instance.Info($"VmSolution.Load完成，耗时: {sw.ElapsedMilliseconds}ms", "VisionMaster");
                });

                // 等待加载，超时60秒
                if (await System.Threading.Tasks.Task.WhenAny(loadTask, System.Threading.Tasks.Task.Delay(60000)) != loadTask)
                {
                    throw new TimeoutException("加载方案超时（60秒），请检查VisionMaster服务是否正常运行");
                }

                // 检查是否有异常
                await loadTask;
                FileLogger.Instance.Info("Task.Run 完成，开始处理结果", "VisionMaster");

                _isSolutionLoaded = true;

                // UI更新必须在Dispatcher中执行
                Dispatcher.Invoke(() =>
                {
                    txtSolutionPath.Text = System.IO.Path.GetFileName(solutionPath);
                });

                // 列出所有流程名称
                var processInfoList = VmSolution.Instance.GetAllProcedureList();
                string allProcessNames = "";
                for (int i = 0; i < processInfoList.nNum; i++)
                {
                    allProcessNames += processInfoList.astProcessInfo[i].strProcessName + ", ";
                }
                FileLogger.Instance.Info($"方案中的流程列表: {allProcessNames}", "VisionMaster");

                // 获取流程
                _station1Procedure = VmSolution.Instance[STATION1_NAME] as VmProcedure;
                _station2Procedure = VmSolution.Instance[STATION2_NAME] as VmProcedure;

                bool station1Ok = _station1Procedure != null;
                bool station2Ok = _station2Procedure != null;

                if (station1Ok)
                    FileLogger.Instance.Info($"已找到流程: {STATION1_NAME}", "VisionMaster");
                if (station2Ok)
                    FileLogger.Instance.Info($"已找到流程: {STATION2_NAME}", "VisionMaster");

                // 更新状态
                int processCount = (int)processInfoList.nNum;
                FileLogger.Instance.Info($"准备更新UI状态，流程数: {processCount}", "VisionMaster");

                UpdateStatus(true, "方案已加载");
                ShowMessage($"方案加载成功，包含 {processCount} 个流程", false);

                FileLogger.Instance.Info($"VisionMaster方案已加载: {solutionPath}", "VisionMaster");

                // 发布Vision状态事件
                EventAggregator.Instance.Publish(new ConnectionChangedEvent
                {
                    DeviceType = "Vision",
                    DeviceName = "VisionMaster",
                    IsConnected = true
                });

                // 加载方案后设置旋转角度
                SetRotationAngleFromPlc();
            }
            catch (VmException ex)
            {
                UpdateStatus(false, "加载失败");
                ShowMessage($"加载方案失败: 错误码 0x{ex.errorCode:X}", true);
                FileLogger.Instance.Error($"加载VisionMaster方案失败: 0x{ex.errorCode:X}", null, "VisionMaster");
            }
            catch (Exception ex)
            {
                UpdateStatus(false, "加载失败");
                ShowMessage($"加载方案失败: {ex.Message}", true);
                FileLogger.Instance.Error($"加载VisionMaster方案失败: {ex.Message}", ex, "VisionMaster");
            }
            finally
            {
                // 等待一下让BeginInvoke的委托有机会执行
                await System.Threading.Tasks.Task.Delay(50);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    btnLoadSolution.IsEnabled = true;
                    Mouse.OverrideCursor = null;
                }));
            }
        }

        /// <summary>
        /// 执行一次按钮点击
        /// </summary>
        private void BtnExecuteOnce_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSolutionLoaded)
            {
                ShowMessage("请先加载方案", true);
                return;
            }

            try
            {
                ShowMessage("正在执行...", false);

                // 执行两个流程
                if (_station1Procedure != null)
                {
                    var startTime = DateTime.Now;
                    _station1Procedure.Run();
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    FileLogger.Instance.Info($"{STATION1_NAME} 执行完成，耗时: {elapsed:F1}ms", "VisionMaster");
                }

                if (_station2Procedure != null)
                {
                    // 执行瓶身工位前设置旋转角度
                    SetRotationAngleFromPlc();

                    var startTime = DateTime.Now;
                    _station2Procedure.Run();
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    FileLogger.Instance.Info($"{STATION2_NAME} 执行完成，耗时: {elapsed:F1}ms", "VisionMaster");
                }

                ShowMessage("执行完成", false);
            }
            catch (VmException ex)
            {
                ShowMessage($"执行失败: 错误码 0x{ex.errorCode:X}", true);
                FileLogger.Instance.Error($"执行VisionMaster流程失败: 0x{ex.errorCode:X}", null, "VisionMaster");
            }
            catch (Exception ex)
            {
                ShowMessage($"执行失败: {ex.Message}", true);
                FileLogger.Instance.Error($"执行VisionMaster流程失败: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 保存方案按钮点击
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSolutionLoaded)
            {
                ShowMessage("请先加载方案", true);
                return;
            }

            try
            {
                VmSolution.Save();
                ShowMessage("方案保存成功", false);
                FileLogger.Instance.Info("VisionMaster方案已保存", "VisionMaster");
            }
            catch (VmException ex)
            {
                ShowMessage($"保存失败: 错误码 0x{ex.errorCode:X}", true);
                FileLogger.Instance.Error($"保存VisionMaster方案失败: 0x{ex.errorCode:X}", null, "VisionMaster");
            }
            catch (Exception ex)
            {
                ShowMessage($"保存失败: {ex.Message}", true);
                FileLogger.Instance.Error($"保存VisionMaster方案失败: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 更新状态指示
        /// </summary>
        private void UpdateStatus(bool isConnected, string message)
        {
            FileLogger.Instance.Debug($"UpdateStatus调用: {message}, 已连接: {isConnected}", "VisionMaster");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                statusIndicator.Fill = isConnected
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
                    : new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
                txtStatus.Text = message;
            }));
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message, bool isError)
        {
            FileLogger.Instance.Debug($"ShowMessage调用: {message}", "VisionMaster");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtMessage.Text = message;
                txtMessage.Foreground = isError
                    ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
                    : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                FileLogger.Instance.Debug($"ShowMessage已更新UI: {message}", "VisionMaster");
            }));
        }

        /// <summary>
        /// 从PLC读取旋转角度并设置到VisionMaster全局变量
        /// </summary>
        private void SetRotationAngleFromPlc()
        {
            try
            {
                var plcConfig = ConfigManager.Instance.Plc;
                var rotationAngleConfig = plcConfig.InputAddresses.RotationAngle;
                if (rotationAngleConfig == null || string.IsNullOrEmpty(rotationAngleConfig.Address))
                {
                    FileLogger.Instance.Warning("旋转角度地址未配置", "VisionMaster");
                    return;
                }

                // 创建临时PLC连接读取旋转角度
                using (var plc = new OmronFinsCommunication(plcConfig.Connection.IP, plcConfig.Connection.Port))
                {
                    var connected = plc.ConnectAsync().Result;
                    if (!connected)
                    {
                        FileLogger.Instance.Warning("无法连接PLC读取旋转角度", "VisionMaster");
                        return;
                    }

                    var rotationAngle = plc.ReadFloatAsync(rotationAngleConfig.Address).Result;
                    FileLogger.Instance.Info($"从PLC读取旋转角度: {rotationAngle:F2}", "VisionMaster");

                    // 设置到全局变量
                    var globalVar = VmSolution.Instance["全局变量1"] as GlobalVariableModuleTool;
                    if (globalVar != null)
                    {
                        globalVar.SetGlobalVar("旋转角度", rotationAngle.ToString("F2"));
                        string readBack = globalVar.GetGlobalVar("旋转角度");
                        FileLogger.Instance.Info($"设置全局变量: 旋转角度={rotationAngle:F2}, 读回={readBack}", "VisionMaster");
                    }
                    else
                    {
                        FileLogger.Instance.Warning("未找到全局变量模块 '全局变量1'", "VisionMaster");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"设置旋转角度失败: {ex.Message}", "VisionMaster");
            }
        }
    }
}
