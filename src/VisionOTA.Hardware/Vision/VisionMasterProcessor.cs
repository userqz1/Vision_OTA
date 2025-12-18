using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Logging;

// VisionMaster SDK
using VM.Core;
using VM.PlatformSDKCS;

namespace VisionOTA.Hardware.Vision
{
    /// <summary>
    /// VisionMaster视觉处理器实现
    /// </summary>
    public class VisionMasterProcessor : IVisionProcessor
    {
        private VmProcedure _procedure;
        private string _procedureName;
        private double _scoreThreshold = 0.5;
        private VisionResult _lastResult;
        private bool _isLoaded;
        private readonly int _stationId;

        // 输出变量名配置
        private string _angleOutputName = "瓶底角度";
        private string _resultImageOutputName = "瓶底结果图";

        public bool IsLoaded => _isLoaded;

        public event EventHandler<VisionProcessCompletedEventArgs> ProcessCompleted;
        public event EventHandler<Exception> ProcessError;

        /// <summary>
        /// 流程名称
        /// </summary>
        public string ProcedureName => _procedureName;

        /// <summary>
        /// 获取VisionMaster流程对象（用于控件绑定）
        /// </summary>
        public VmProcedure Procedure => _procedure;

        public VisionMasterProcessor(int stationId)
        {
            _stationId = stationId;
        }

        /// <summary>
        /// 配置输出变量名
        /// </summary>
        /// <param name="angleOutputName">角度输出变量名</param>
        /// <param name="resultImageOutputName">结果图输出变量名</param>
        public void ConfigureOutputs(string angleOutputName, string resultImageOutputName)
        {
            _angleOutputName = angleOutputName;
            _resultImageOutputName = resultImageOutputName;
            FileLogger.Instance.Info($"工位{_stationId}输出配置: 角度={angleOutputName}, 结果图={resultImageOutputName}", "VisionMaster");
        }

        /// <summary>
        /// 加载VisionMaster流程（通过方案中的流程名称）
        /// </summary>
        /// <param name="procedureName">流程名称，如"瓶底定位"</param>
        /// <returns>是否成功</returns>
        public bool LoadToolBlock(string procedureName)
        {
            try
            {
                if (VmSolution.Instance == null)
                {
                    FileLogger.Instance.Error("VisionMaster方案未加载", null, "VisionMaster");
                    return false;
                }

                _procedure = VmSolution.Instance[procedureName] as VmProcedure;
                if (_procedure == null)
                {
                    FileLogger.Instance.Error($"未找到流程: {procedureName}", null, "VisionMaster");
                    return false;
                }

                _procedureName = procedureName;
                _isLoaded = true;
                FileLogger.Instance.Info($"VisionMaster流程已加载: {procedureName}", "VisionMaster");
                return true;
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"加载VisionMaster流程失败: 0x{ex.errorCode:X}", null, "VisionMaster");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"加载VisionMaster流程失败: {ex.Message}", ex, "VisionMaster");
                return false;
            }
        }

        public void UnloadToolBlock()
        {
            _procedure = null;
            _procedureName = null;
            _isLoaded = false;
            FileLogger.Instance.Info("VisionMaster流程已卸载", "VisionMaster");
        }

        public VisionResult Execute(Bitmap image)
        {
            var result = new VisionResult();
            var startTime = DateTime.Now;

            try
            {
                if (!_isLoaded || _procedure == null)
                {
                    result.Found = false;
                    result.ErrorMessage = "流程未加载";
                    return result;
                }

                // 设置输入图像（如果需要从外部传入图像）
                if (image != null)
                {
                    SetInputImage(image);
                }

                // 执行流程
                _procedure.Run();

                // 提取结果
                ExtractResult(result);

                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                _lastResult = result;

                ProcessCompleted?.Invoke(this, new VisionProcessCompletedEventArgs
                {
                    Result = result,
                    StationId = _stationId
                });
            }
            catch (VmException ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: 0x{ex.errorCode:X}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error(result.ErrorMessage, null, "VisionMaster");
                ProcessError?.Invoke(this, ex);
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: {ex.Message}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error(result.ErrorMessage, ex, "VisionMaster");
                ProcessError?.Invoke(this, ex);
            }

            return result;
        }

        /// <summary>
        /// 设置输入图像到图像源模块（VisionMaster流程内部已配置相机，通常不需要外部传入）
        /// </summary>
        private void SetInputImage(Bitmap image)
        {
            // VisionMaster流程内部已配置相机触发，不需要从外部设置图像
            // 如果需要从SDK设置图像，可在此扩展
            FileLogger.Instance.Debug($"收到外部图像: {image?.Width}x{image?.Height}，使用流程内部图像源", "VisionMaster");
        }

        /// <summary>
        /// 从流程输出提取结果
        /// </summary>
        private void ExtractResult(VisionResult result)
        {
            try
            {
                // 从流程输出设置获取角度值
                // 路径格式: 流程名.Outputs.变量名.Value
                var anglePath = $"{_procedureName}.Outputs.{_angleOutputName}.Value";

                var angleValue = VmSolution.Instance[anglePath];
                if (angleValue != null)
                {
                    if (angleValue is Array angleArray && angleArray.Length > 0)
                    {
                        result.Angle = Convert.ToDouble(angleArray.GetValue(0));
                    }
                    else
                    {
                        result.Angle = Convert.ToDouble(angleValue);
                    }

                    result.Found = true;
                    FileLogger.Instance.Debug($"工位{_stationId}结果: 角度={result.Angle:F2}", "VisionMaster");
                }
                else
                {
                    FileLogger.Instance.Warning($"未获取到角度值，路径: {anglePath}", "VisionMaster");
                    result.Found = false;
                    result.ErrorMessage = "未获取到角度值";
                }

                // 获取输出图像
                result.ResultImage = GetOutputImage();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"提取结果失败: {ex.Message}", "VisionMaster");
                result.Found = false;
                result.ErrorMessage = $"结果提取失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取输出图像（从流程输出变量）
        /// </summary>
        private Bitmap GetOutputImage()
        {
            try
            {
                if (string.IsNullOrEmpty(_resultImageOutputName))
                {
                    return null;
                }

                // 从流程输出设置获取结果图
                // 路径格式: 流程名.Outputs.变量名.Value
                var imagePath = $"{_procedureName}.Outputs.{_resultImageOutputName}.Value";
                var imageValue = VmSolution.Instance[imagePath];

                if (imageValue == null)
                {
                    FileLogger.Instance.Debug($"未获取到结果图，路径: {imagePath}", "VisionMaster");
                    return null;
                }

                // 使用 VisionMaster 提供的方法获取图像
                var vmImageData = _procedure.GetVmIOImageValue(_resultImageOutputName);
                if (vmImageData != null)
                {
                    return ConvertVmImageToBitmap(vmImageData);
                }

                FileLogger.Instance.Debug($"结果图类型不支持: {imageValue.GetType().Name}", "VisionMaster");
                return null;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"获取输出图像失败: {ex.Message}", "VisionMaster");
                return null;
            }
        }

        /// <summary>
        /// 转换VisionMaster图像数据为Bitmap
        /// </summary>
        private Bitmap ConvertVmImageToBitmap(IVmImageData vmImageData)
        {
            try
            {
                if (vmImageData == null || vmImageData.Data == IntPtr.Zero)
                {
                    return null;
                }

                int width = vmImageData.Width;
                int height = vmImageData.Height;

                // 计算stride（4字节对齐）
                int stride = ((width + 3) / 4) * 4;

                var bitmap = new Bitmap(width, height, stride, PixelFormat.Format8bppIndexed, vmImageData.Data);

                // 设置灰度调色板
                var palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;

                return bitmap;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"图像转换失败: {ex.Message}", ex, "VisionMaster");
                return null;
            }
        }

        /// <summary>
        /// 执行一次流程（无需输入图像，使用流程内配置的图像源）
        /// </summary>
        public VisionResult ExecuteOnce()
        {
            return Execute(null);
        }

        public VisionResult GetLastResult() => _lastResult;

        public void SetScoreThreshold(double threshold)
        {
            _scoreThreshold = threshold;
        }

        public void Dispose()
        {
            UnloadToolBlock();
        }
    }

    /// <summary>
    /// VisionMaster方案管理器（单例）
    /// </summary>
    public class VisionMasterSolutionManager : IDisposable
    {
        private static VisionMasterSolutionManager _instance;
        private static readonly object _lock = new object();

        private bool _isSolutionLoaded;
        private string _solutionPath;

        public static VisionMasterSolutionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VisionMasterSolutionManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool IsSolutionLoaded => _isSolutionLoaded;
        public string SolutionPath => _solutionPath;

        private VisionMasterSolutionManager()
        {
        }

        /// <summary>
        /// 加载VisionMaster方案
        /// </summary>
        /// <param name="solutionPath">.sol文件路径</param>
        /// <param name="password">密码（可选）</param>
        public bool LoadSolution(string solutionPath, string password = "")
        {
            try
            {
                if (_isSolutionLoaded)
                {
                    CloseSolution();
                }

                FileLogger.Instance.Info($"正在加载VisionMaster方案: {solutionPath}", "VisionMaster");
                VmSolution.Load(solutionPath, password);

                // 禁用所有模块回调以提高性能
                VmSolution.Instance.DisableModulesCallback();

                _solutionPath = solutionPath;
                _isSolutionLoaded = true;

                FileLogger.Instance.Info($"VisionMaster方案已加载: {solutionPath}", "VisionMaster");
                return true;
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"加载VisionMaster方案失败: 0x{ex.errorCode:X}", null, "VisionMaster");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"加载VisionMaster方案失败: {ex.Message}", ex, "VisionMaster");
                return false;
            }
        }

        /// <summary>
        /// 保存方案
        /// </summary>
        public bool SaveSolution()
        {
            try
            {
                if (!_isSolutionLoaded)
                    return false;

                VmSolution.Save();
                FileLogger.Instance.Info("VisionMaster方案已保存", "VisionMaster");
                return true;
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"保存VisionMaster方案失败: 0x{ex.errorCode:X}", null, "VisionMaster");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"保存VisionMaster方案失败: {ex.Message}", ex, "VisionMaster");
                return false;
            }
        }

        /// <summary>
        /// 关闭方案
        /// </summary>
        public void CloseSolution()
        {
            try
            {
                if (_isSolutionLoaded && VmSolution.Instance != null)
                {
                    VmSolution.Instance.CloseSolution();
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"关闭VisionMaster方案失败: {ex.Message}", ex, "VisionMaster");
            }
            finally
            {
                _isSolutionLoaded = false;
                _solutionPath = null;
            }
        }

        /// <summary>
        /// 获取流程
        /// </summary>
        public VmProcedure GetProcedure(string procedureName)
        {
            if (!_isSolutionLoaded || VmSolution.Instance == null)
                return null;

            return VmSolution.Instance[procedureName] as VmProcedure;
        }

        public void Dispose()
        {
            try
            {
                CloseSolution();
                // 注意：VmSolution.Instance?.Dispose() 可能阻塞，移到后台线程
                if (VmSolution.Instance != null)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            VmSolution.Instance?.Dispose();
                        }
                        catch { }
                    }).Wait(3000); // 最多等待3秒
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"VisionMaster清理失败: {ex.Message}", ex, "VisionMaster");
            }
        }
    }
}
