using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Logging;

// VisionMaster SDK
using VM.Core;
using VM.PlatformSDKCS;
using GlobalVariableModuleCs;
using ImageSourceModuleCs;

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

        // 输入图像源名称配置
        private string _inputImageSourceName = "图像源";

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
        /// 配置输入图像源名称
        /// </summary>
        /// <param name="imageSourceName">VisionMaster流程中图像源模块的名称</param>
        public void ConfigureInputImageSource(string imageSourceName)
        {
            _inputImageSourceName = imageSourceName;
            FileLogger.Instance.Info($"工位{_stationId}输入图像源配置: {imageSourceName}", "VisionMaster");
        }

        /// <summary>
        /// 设置流程输入参数（通过全局变量）
        /// </summary>
        /// <param name="parameterName">参数名称（全局变量名）</param>
        /// <param name="value">参数值</param>
        /// <returns>是否成功</returns>
        public bool SetInputParameter(string parameterName, float value)
        {
            try
            {
                FileLogger.Instance.Debug($"SetInputParameter开始: parameterName={parameterName}, value={value}", "VisionMaster");

                if (VmSolution.Instance == null)
                {
                    FileLogger.Instance.Warning("VmSolution.Instance为空", "VisionMaster");
                    return false;
                }

                // 通过全局变量设置参数
                var obj = VmSolution.Instance["全局变量1"];
                FileLogger.Instance.Debug($"VmSolution.Instance[全局变量1] 类型: {obj?.GetType().Name ?? "null"}", "VisionMaster");

                GlobalVariableModuleTool globalVar = obj as GlobalVariableModuleTool;

                if (globalVar != null)
                {
                    // 设置变量值
                    globalVar.SetGlobalVar("旋转角度", value.ToString("F2"));

                    // 读回验证
                    string readBack = globalVar.GetGlobalVar("旋转角度");
                    FileLogger.Instance.Info($"工位{_stationId}设置全局变量: 旋转角度={value:F2}, 读回={readBack}", "VisionMaster");

                    return true;
                }
                else
                {
                    FileLogger.Instance.Warning($"未找到全局变量模块 '全局变量1', obj={obj?.GetType().Name ?? "null"}", "VisionMaster");
                    return false;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置全局变量失败: {ex.Message}", ex, "VisionMaster");
                return false;
            }
        }

        /// <summary>
        /// 获取流程输入参数值
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>参数值，获取失败返回null</returns>
        public float? GetInputParameter(string parameterName)
        {
            try
            {
                if (!_isLoaded || _procedure == null)
                {
                    FileLogger.Instance.Warning($"流程未加载，无法获取输入参数: {parameterName}", "VisionMaster");
                    return null;
                }

                // 从流程输入设置获取参数值
                var inputPath = $"{_procedureName}.Inputs.{parameterName}.Value";
                var value = VmSolution.Instance[inputPath];

                if (value != null)
                {
                    return Convert.ToSingle(value);
                }

                return null;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"获取输入参数失败: {ex.Message}, 参数名={parameterName}", ex, "VisionMaster");
                return null;
            }
        }

        /// <summary>
        /// 加载VisionMaster流程（通过方案中的流程名称）
        /// </summary>
        /// <param name="procedureName">流程名称，如"瓶底定位"</param>
        /// <returns>是否成功</returns>
        public bool LoadToolBlock(string procedureName)
        {
            FileLogger.Instance.Info($"工位{_stationId}正在加载流程: {procedureName}", "VisionMaster");

            try
            {
                if (VmSolution.Instance == null)
                {
                    FileLogger.Instance.Error($"工位{_stationId}加载流程失败: VisionMaster方案未加载", null, "VisionMaster");
                    return false;
                }

                _procedure = VmSolution.Instance[procedureName] as VmProcedure;
                if (_procedure == null)
                {
                    FileLogger.Instance.Error($"工位{_stationId}未找到流程: '{procedureName}' (请检查VisionMaster方案中流程名称是否正确)", null, "VisionMaster");
                    return false;
                }

                _procedureName = procedureName;
                _isLoaded = true;
                FileLogger.Instance.Info($"工位{_stationId}流程加载成功: {procedureName}, 输入图像源={_inputImageSourceName}, 角度输出={_angleOutputName}", "VisionMaster");

                // 打印流程模块结构（调试用）
                VisionMasterSolutionManager.Instance.PrintProcedureModules(procedureName);

                return true;
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}加载流程异常: 错误码=0x{ex.errorCode:X}, 流程名={procedureName}", null, "VisionMaster");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}加载流程异常: {ex.Message}, 流程名={procedureName}", ex, "VisionMaster");
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

                // 设置输入图像
                if (image != null)
                {
                    SetInputImage(image);
                }

                // 执行流程
                _procedure.Run();

                // 只提取角度（不获取结果图，加快返回速度）
                ExtractAngleOnly(result);

                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                _lastResult = result;

                FileLogger.Instance.Info($"工位{_stationId}视觉处理完成: Found={result.Found}, Angle={result.Angle:F2}, 耗时={result.ProcessTimeMs:F0}ms", "VisionMaster");
            }
            catch (VmException ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: 0x{ex.errorCode:X}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error($"工位{_stationId} VisionMaster异常: 0x{ex.errorCode:X}", null, "VisionMaster");
                ProcessError?.Invoke(this, ex);
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: {ex.Message}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error($"工位{_stationId}执行异常: {ex.Message}", ex, "VisionMaster");
                ProcessError?.Invoke(this, ex);
            }

            return result;
        }

        /// <summary>
        /// 设置输入图像到图像源模块（文件路径方式）
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        public void SetInputImageFromFile(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _procedure == null)
            {
                FileLogger.Instance.Warning($"工位{_stationId} SetInputImageFromFile: 路径={imagePath}, 流程={_procedure != null}", "VisionMaster");
                return;
            }

            try
            {
                // 获取图像源模块
                var imageSource = VmSolution.Instance[$"{_procedureName}.{_inputImageSourceName}"] as ImageSourceModuleTool;
                if (imageSource == null)
                {
                    FileLogger.Instance.Error($"工位{_stationId}未找到图像源模块: {_procedureName}.{_inputImageSourceName}", null, "VisionMaster");
                    return;
                }

                // 使用SetImagePath直接加载文件
                FileLogger.Instance.Debug($"工位{_stationId}调用SetImagePath: {imagePath}", "VisionMaster");
                imageSource.SetImagePath(imagePath);
                FileLogger.Instance.Info($"工位{_stationId}输入图像设置成功(文件): {imagePath}", "VisionMaster");
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置输入图像失败: 错误码=0x{ex.errorCode:X}, 路径={imagePath}", null, "VisionMaster");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置输入图像异常: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 设置输入图像到图像源模块（SDK方式）
        /// </summary>
        private void SetInputImage(Bitmap image)
        {
            if (image == null || _procedure == null)
            {
                FileLogger.Instance.Warning($"工位{_stationId} SetInputImage: 图像={image != null}, 流程={_procedure != null}", "VisionMaster");
                return;
            }

            try
            {
                // 获取图像源模块
                var imageSource = VmSolution.Instance[$"{_procedureName}.{_inputImageSourceName}"] as ImageSourceModuleTool;
                if (imageSource == null)
                {
                    FileLogger.Instance.Error($"工位{_stationId}未找到图像源模块: {_procedureName}.{_inputImageSourceName}", null, "VisionMaster");
                    return;
                }

                // 将Bitmap转换为VisionMaster可用的图像数据
                var rect = new Rectangle(0, 0, image.Width, image.Height);

                // 确定像素格式并转换图像
                Bitmap processImage = image;
                ImageSourceParam.PixelFormatEnum vmPixelFormat;
                int bytesPerPixel;

                switch (image.PixelFormat)
                {
                    case PixelFormat.Format8bppIndexed:
                        vmPixelFormat = ImageSourceParam.PixelFormatEnum.MONO8;
                        bytesPerPixel = 1;
                        break;
                    case PixelFormat.Format24bppRgb:
                        vmPixelFormat = ImageSourceParam.PixelFormatEnum.RGB24;
                        bytesPerPixel = 3;
                        break;
                    default:
                        FileLogger.Instance.Warning($"工位{_stationId}像素格式{image.PixelFormat}需要转换", "VisionMaster");
                        // 转换为24位RGB
                        processImage = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
                        using (var g = Graphics.FromImage(processImage))
                        {
                            g.DrawImage(image, 0, 0, image.Width, image.Height);
                        }
                        vmPixelFormat = ImageSourceParam.PixelFormatEnum.RGB24;
                        bytesPerPixel = 3;
                        break;
                }

                var bitmapData = processImage.LockBits(rect, ImageLockMode.ReadOnly, processImage.PixelFormat);
                try
                {
                    int width = processImage.Width;
                    int height = processImage.Height;
                    int stride = bitmapData.Stride;
                    int expectedStride = width * bytesPerPixel;

                    byte[] imageBytes;

                    // 如果stride和预期不一致，需要逐行复制去除padding
                    if (stride != expectedStride)
                    {
                        FileLogger.Instance.Debug($"工位{_stationId}Stride不匹配: 实际={stride}, 预期={expectedStride}, 需要逐行复制", "VisionMaster");
                        imageBytes = new byte[expectedStride * height];
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(bitmapData.Scan0 + y * stride, imageBytes, y * expectedStride, expectedStride);
                        }
                    }
                    else
                    {
                        int dataSize = stride * height;
                        imageBytes = new byte[dataSize];
                        Marshal.Copy(bitmapData.Scan0, imageBytes, 0, dataSize);
                    }

                    // 创建ImageBaseData
                    var imgData = new ImageBaseData
                    {
                        Width = width,
                        Height = height,
                        DataLen = (uint)imageBytes.Length,
                        Pixelformat = (int)vmPixelFormat,
                        ImageData = imageBytes
                    };

                    // 设置图像数据
                    FileLogger.Instance.Debug($"工位{_stationId}调用SetImageData: 尺寸={width}x{height}, 格式={vmPixelFormat}, 数据大小={imageBytes.Length}", "VisionMaster");
                    imageSource.SetImageData(imgData);

                    FileLogger.Instance.Info($"工位{_stationId}输入图像设置成功: {width}x{height}, 图像源={_inputImageSourceName}", "VisionMaster");
                }
                finally
                {
                    processImage.UnlockBits(bitmapData);
                    // 如果是转换后的图像，需要释放
                    if (processImage != image)
                    {
                        processImage.Dispose();
                    }
                }
            }
            catch (VmException ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置输入图像失败: 错误码=0x{ex.errorCode:X}, 图像源名称={_inputImageSourceName} (请检查VisionMaster中图像源模块名称是否匹配)", null, "VisionMaster");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{_stationId}设置输入图像异常: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 从流程输出提取角度（不获取结果图，用于快速返回）
        /// </summary>
        private void ExtractAngleOnly(VisionResult result)
        {
            try
            {
                // 优先从全局变量读取角度值
                double? angleFromGlobal = GetAngleFromGlobalVariable();
                if (angleFromGlobal.HasValue && !double.IsNaN(angleFromGlobal.Value) && !double.IsInfinity(angleFromGlobal.Value))
                {
                    result.Angle = angleFromGlobal.Value;
                    result.Found = true;
                    return;
                }

                // 备选：从流程输出设置获取角度值
                var anglePath = $"{_procedureName}.Outputs.{_angleOutputName}";
                var angleValue = VmSolution.Instance[anglePath];
                if (angleValue != null)
                {
                    double? extractedAngle = null;
                    var valueProperty = angleValue.GetType().GetProperty("Value");
                    if (valueProperty != null)
                    {
                        var actualValue = valueProperty.GetValue(angleValue);
                        if (actualValue is Array arr && arr.Length > 0)
                        {
                            var firstElement = arr.GetValue(0);
                            if (firstElement != null)
                            {
                                extractedAngle = Convert.ToDouble(firstElement);
                            }
                        }
                        else if (actualValue != null)
                        {
                            extractedAngle = Convert.ToDouble(actualValue);
                        }
                    }
                    else if (angleValue is Array angleArray && angleArray.Length > 0)
                    {
                        extractedAngle = Convert.ToDouble(angleArray.GetValue(0));
                    }
                    else
                    {
                        extractedAngle = Convert.ToDouble(angleValue);
                    }

                    if (extractedAngle.HasValue && !double.IsNaN(extractedAngle.Value) && !double.IsInfinity(extractedAngle.Value))
                    {
                        result.Angle = extractedAngle.Value;
                        result.Found = true;
                    }
                    else
                    {
                        result.Found = false;
                        result.ErrorMessage = "角度值无效(NaN或Infinity)";
                    }
                }
                else
                {
                    result.Found = false;
                    result.ErrorMessage = $"未获取到角度值(变量名:{_angleOutputName})";
                }
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"角度提取失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 填充结果图像（在PLC写入后调用）
        /// </summary>
        public void FillResultImage(VisionResult result, Bitmap originalImage)
        {
            try
            {
                result.ResultImage = GetOutputImage();

                // 如果没有结果图像（NG时），使用原图
                if (result.ResultImage == null && originalImage != null)
                {
                    result.ResultImage = (Bitmap)originalImage.Clone();
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"工位{_stationId}获取结果图失败: {ex.Message}", "VisionMaster");
            }
        }

        /// <summary>
        /// 从全局变量读取角度值（只读取配置的变量名）
        /// </summary>
        private double? GetAngleFromGlobalVariable()
        {
            try
            {
                var globalVar = VmSolution.Instance["全局变量1"] as GlobalVariableModuleTool;
                if (globalVar == null)
                {
                    return null;
                }

                // 只读取配置的角度输出变量名
                string valueStr = globalVar.GetGlobalVar(_angleOutputName);
                if (!string.IsNullOrEmpty(valueStr) && double.TryParse(valueStr, out double angle))
                {
                    FileLogger.Instance.Debug($"工位{_stationId}从全局变量'{_angleOutputName}'读取角度: {angle:F2}", "VisionMaster");
                    return angle;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取输出图像（从流程输出变量）
        /// </summary>
        private Bitmap GetOutputImage()
        {
            try
            {
                if (string.IsNullOrEmpty(_resultImageOutputName) || _procedure == null)
                {
                    return null;
                }

                // 使用流程的 GetVmIOImageValue 方法获取结果图
                var vmImage = _procedure.GetVmIOImageValue(_resultImageOutputName);
                if (vmImage != null)
                {
                    return ConvertVmImageToBitmap(vmImage);
                }

                FileLogger.Instance.Debug($"未获取到结果图，变量名: {_resultImageOutputName}", "VisionMaster");
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
                var imageFormat = vmImageData.ImageFormat;

                FileLogger.Instance.Debug($"转换VisionMaster图像: {width}x{height}, ImageFormat={imageFormat}", "VisionMaster");

                // 根据像素格式创建对应的Bitmap
                // ImageFormatEnum: NULL=0, MONO8=1, RGB24=2
                if (imageFormat == ImageFormatEnum.IMAGE_PIXEL_FORMAT_RGB24)
                {
                    // RGB24: 每像素3字节
                    int bytesPerPixel = 3;
                    int stride = width * bytesPerPixel;
                    // 4字节对齐
                    int alignedStride = ((stride + 3) / 4) * 4;

                    // 如果数据stride与Bitmap stride不一致，需要逐行复制
                    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    var rect = new Rectangle(0, 0, width, height);
                    var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                    try
                    {
                        // VisionMaster返回的是连续数据，Bitmap可能有padding
                        for (int y = 0; y < height; y++)
                        {
                            IntPtr srcPtr = vmImageData.Data + y * stride;
                            IntPtr dstPtr = bmpData.Scan0 + y * bmpData.Stride;
                            // 复制一行数据（只复制有效像素，不包括padding）
                            byte[] rowData = new byte[stride];
                            Marshal.Copy(srcPtr, rowData, 0, stride);
                            Marshal.Copy(rowData, 0, dstPtr, stride);
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bmpData);
                    }

                    FileLogger.Instance.Debug($"RGB24图像转换成功: {width}x{height}", "VisionMaster");
                    return bitmap;
                }
                else // MONO8 或其他灰度格式
                {
                    // 灰度图: 每像素1字节，stride需4字节对齐
                    int stride = ((width + 3) / 4) * 4;

                    var bitmap = new Bitmap(width, height, stride, PixelFormat.Format8bppIndexed, vmImageData.Data);

                    // 设置灰度调色板
                    var palette = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmap.Palette = palette;

                    FileLogger.Instance.Debug($"灰度图像转换成功: {width}x{height}", "VisionMaster");
                    return bitmap;
                }
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

        /// <summary>
        /// 使用图片文件路径执行流程
        /// </summary>
        /// <param name="imagePath">图片文件路径</param>
        /// <returns>视觉处理结果</returns>
        public VisionResult ExecuteWithFilePath(string imagePath)
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

                // 设置输入图像路径
                SetInputImageFromFile(imagePath);

                // 执行流程
                _procedure.Run();

                // 只提取角度
                ExtractAngleOnly(result);

                // 获取结果图（离线测试模式需要立即显示）
                result.ResultImage = GetOutputImage();
                if (result.ResultImage == null && !string.IsNullOrEmpty(imagePath))
                {
                    try
                    {
                        result.ResultImage = new Bitmap(imagePath);
                    }
                    catch { }
                }

                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                _lastResult = result;

                FileLogger.Instance.Info($"工位{_stationId}视觉处理完成(文件): Found={result.Found}, Angle={result.Angle:F2}, 耗时={result.ProcessTimeMs:F0}ms", "VisionMaster");
            }
            catch (VmException ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: 0x{ex.errorCode:X}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error($"工位{_stationId} VisionMaster异常: 0x{ex.errorCode:X}", null, "VisionMaster");
                ProcessError?.Invoke(this, ex);
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"执行失败: {ex.Message}";
                result.ProcessTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Instance.Error($"工位{_stationId}执行异常: {ex.Message}", ex, "VisionMaster");
                ProcessError?.Invoke(this, ex);
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

                FileLogger.Instance.Info($"正在加载VisionMaster方案: {solutionPath}", "VisionMaster");
                VmSolution.Load(solutionPath, password);

                // 禁用所有模块回调以提高性能
                VmSolution.Instance.DisableModulesCallback();

                _solutionPath = solutionPath;
                _isSolutionLoaded = true;

                FileLogger.Instance.Info($"VisionMaster方案已加载: {solutionPath}", "VisionMaster");

                // 打印方案结构用于调试
                PrintSolutionStructure();

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

        /// <summary>
        /// 打印指定流程的所有模块和参数
        /// </summary>
        public void PrintProcedureModules(string procedureName)
        {
            try
            {
                if (!_isSolutionLoaded || VmSolution.Instance == null)
                {
                    FileLogger.Instance.Warning("方案未加载", "VisionMaster");
                    return;
                }

                var procedure = VmSolution.Instance[procedureName] as VmProcedure;
                if (procedure == null)
                {
                    FileLogger.Instance.Warning($"未找到流程: {procedureName}", "VisionMaster");
                    return;
                }

                FileLogger.Instance.Info($"========== 流程 [{procedureName}] 模块列表 ==========", "VisionMaster");

                // 尝试获取流程的模块列表
                try
                {
                    // 获取流程输出
                    var outputs = procedure.Outputs;
                    if (outputs != null)
                    {
                        FileLogger.Instance.Info($"流程输出数量: {outputs.Count}", "VisionMaster");
                        for (int i = 0; i < outputs.Count; i++)
                        {
                            try
                            {
                                var output = outputs[i];
                                FileLogger.Instance.Info($"  输出[{i}]: {output?.ToString() ?? "null"}", "VisionMaster");
                            }
                            catch (Exception ex)
                            {
                                FileLogger.Instance.Debug($"  输出[{i}]: 读取失败 - {ex.Message}", "VisionMaster");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Debug($"获取流程输出失败: {ex.Message}", "VisionMaster");
                }

                // 尝试常见模块名称
                string[] moduleNames = new[]
                {
                    "图像源1", "图像源2", "图像源",
                    "脚本1", "脚本2", "脚本",
                    "Script1", "Script2",
                    "全局变量1", "全局变量",
                    "匹配1", "匹配2",
                    "结果输出", "输出"
                };

                foreach (var moduleName in moduleNames)
                {
                    try
                    {
                        var module = VmSolution.Instance[$"{procedureName}.{moduleName}"];
                        if (module != null)
                        {
                            FileLogger.Instance.Info($"  模块: {moduleName}, 类型: {module.GetType().Name}", "VisionMaster");

                            // 尝试获取ModuResult
                            try
                            {
                                var moduResult = module.GetType().GetProperty("ModuResult")?.GetValue(module);
                                if (moduResult != null)
                                {
                                    FileLogger.Instance.Info($"    ModuResult: {moduResult.GetType().Name}", "VisionMaster");

                                    // 尝试获取result参数
                                    try
                                    {
                                        var getOutputFloat = moduResult.GetType().GetMethod("GetOutputFloat");
                                        if (getOutputFloat != null)
                                        {
                                            var resultValue = getOutputFloat.Invoke(moduResult, new object[] { "result" });
                                            FileLogger.Instance.Info($"    result参数值: {resultValue}", "VisionMaster");
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                FileLogger.Instance.Info($"========== 流程 [{procedureName}] 探测完成 ==========", "VisionMaster");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"打印流程模块失败: {ex.Message}", ex, "VisionMaster");
            }
        }

        /// <summary>
        /// 打印方案结构（用于调试）- 尝试常见流程名称
        /// </summary>
        public void PrintSolutionStructure()
        {
            try
            {
                if (!_isSolutionLoaded || VmSolution.Instance == null)
                {
                    FileLogger.Instance.Warning("方案未加载，无法打印结构", "VisionMaster");
                    return;
                }

                FileLogger.Instance.Info("========== VisionMaster方案结构探测 ==========", "VisionMaster");

                // 尝试常见的流程名称
                string[] possibleProcessNames = new[]
                {
                    "瓶底工位", "瓶身工位",
                    "流程1", "流程2", "流程3",
                    "10001流程", "流程10001",
                    "Process1", "Process2",
                    "主流程", "流程"
                };

                foreach (var processName in possibleProcessNames)
                {
                    try
                    {
                        var process = VmSolution.Instance[processName] as VmProcedure;
                        if (process != null)
                        {
                            FileLogger.Instance.Info($"【找到流程】{processName} ✓", "VisionMaster");

                            // 尝试常见的图像源名称
                            string[] possibleImageSourceNames = new[]
                            {
                                "图像源", "图像源1", "图像源2",
                                "4图像源1", "4图像源2",
                                "ImageSource", "ImageSource1"
                            };

                            foreach (var imgSrcName in possibleImageSourceNames)
                            {
                                try
                                {
                                    var imgSrc = VmSolution.Instance[$"{processName}.{imgSrcName}"] as ImageSourceModuleTool;
                                    if (imgSrc != null)
                                    {
                                        FileLogger.Instance.Info($"  ├─ 图像源模块: {imgSrcName} ✓", "VisionMaster");
                                    }
                                }
                                catch { }
                            }

                            // 尝试常见的输出变量名称
                            string[] possibleOutputNames = new[]
                            {
                                "瓶底角度", "瓶身角度", "角度", "Angle",
                                "瓶底结果图", "瓶身结果图", "结果图", "ResultImage",
                                "匹配分数", "Score", "X", "Y"
                            };

                            foreach (var outputName in possibleOutputNames)
                            {
                                try
                                {
                                    var outputPath = $"{processName}.Outputs.{outputName}.Value";
                                    var outputValue = VmSolution.Instance[outputPath];
                                    if (outputValue != null)
                                    {
                                        FileLogger.Instance.Info($"  ├─ 输出变量: {outputName} = {outputValue} ✓", "VisionMaster");
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                FileLogger.Instance.Info("========== 方案结构探测完成 ==========", "VisionMaster");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"打印方案结构失败: {ex.Message}", ex, "VisionMaster");
            }
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
