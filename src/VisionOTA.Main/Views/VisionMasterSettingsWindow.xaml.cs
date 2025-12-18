using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using VM.Core;
using VM.PlatformSDKCS;
using GlobalVariableModuleCs;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 尝试自动加载默认方案
                if (System.IO.File.Exists(DEFAULT_SOLUTION_PATH))
                {
                    LoadSolution(DEFAULT_SOLUTION_PATH);
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 注意：不在这里关闭方案，因为方案可能被其他地方使用
        }

        /// <summary>
        /// 加载方案按钮点击
        /// </summary>
        private void BtnLoadSolution_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择VisionMaster方案文件",
                Filter = "VisionMaster方案|*.sol|所有文件|*.*",
                InitialDirectory = System.IO.Path.GetDirectoryName(DEFAULT_SOLUTION_PATH)
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSolution(dialog.FileName);
            }
        }

        /// <summary>
        /// 加载方案
        /// </summary>
        private void LoadSolution(string solutionPath)
        {
            try
            {
                ShowMessage("正在加载方案...", false);
                this.IsEnabled = false;

                // 如果已加载方案，先关闭
                if (_isSolutionLoaded)
                {
                    try
                    {
                        VmSolution.Instance?.CloseSolution();
                    }
                    catch { }
                    _isSolutionLoaded = false;
                }

                // 加载新方案
                VmSolution.Load(solutionPath);
                _isSolutionLoaded = true;

                txtSolutionPath.Text = System.IO.Path.GetFileName(solutionPath);

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

                // VmMainViewConfigControl 会自动显示加载的方案流程图
                // 不需要手动绑定，加载方案后控件会自动刷新

                bool station1Ok = _station1Procedure != null;
                bool station2Ok = _station2Procedure != null;

                if (station1Ok)
                    FileLogger.Instance.Info($"已找到流程: {STATION1_NAME}", "VisionMaster");
                if (station2Ok)
                    FileLogger.Instance.Info($"已找到流程: {STATION2_NAME}", "VisionMaster");

                if (station1Ok && station2Ok)
                {
                    UpdateStatus(true, "方案已加载");
                    ShowMessage($"方案加载成功，包含 {processInfoList.nNum} 个流程", false);
                }
                else
                {
                    UpdateStatus(true, "方案已加载");
                    ShowMessage($"方案加载成功，流程数: {processInfoList.nNum}", false);
                }

                FileLogger.Instance.Info($"VisionMaster方案已加载: {solutionPath}", "VisionMaster");

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
                this.IsEnabled = true;
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
            statusIndicator.Fill = isConnected
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
                : new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
            txtStatus.Text = message;
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message, bool isError)
        {
            txtMessage.Text = message;
            txtMessage.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
                : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
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
