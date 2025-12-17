using System;
using System.Drawing;
using VM.Core;
using VM.PlatformSDKCS;
using VisionOTA.Infrastructure.Logging;

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

        public VisionMasterProcessor()
        {
        }

        /// <summary>
        /// 加载VisionMaster流程（通过方案中的流程名称）
        /// </summary>
        /// <param name="procedureName">流程名称，如"瓶底工位"</param>
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

                // TODO: 设置输入图像到流程
                // 需要根据具体流程配置来设置图像输入

                // 执行流程
                _procedure.Run();

                // TODO: 从流程获取结果
                // 需要根据具体流程配置来获取输出结果
                // 示例：
                // var moduleResult = _procedure.ModuResult;
                // result.Angle = moduleResult.GetOutputFloat("angle").fValue;
                // result.Score = moduleResult.GetOutputFloat("score").fValue;

                result.Found = true; // 根据实际结果判断
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

                _lastResult = result;
                ProcessCompleted?.Invoke(this, new VisionProcessCompletedEventArgs
                {
                    Result = result,
                    StationId = 0
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
        /// 执行一次流程（无需输入图像，使用流程内配置的图像源）
        /// </summary>
        public VisionResult ExecuteOnce()
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

                _procedure.Run();

                result.Found = true;
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                _lastResult = result;

                ProcessCompleted?.Invoke(this, new VisionProcessCompletedEventArgs
                {
                    Result = result,
                    StationId = 0
                });
            }
            catch (VmException ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: 0x{ex.errorCode:X}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error(result.ErrorMessage, null, "VisionMaster");
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: {ex.Message}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error(result.ErrorMessage, ex, "VisionMaster");
            }

            return result;
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

                VmSolution.Load(solutionPath, password);
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
            CloseSolution();
            VmSolution.Instance?.Dispose();
        }
    }
}
