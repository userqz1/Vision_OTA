using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Vision
{
    /// <summary>
    /// 模拟视觉处理器
    /// </summary>
    public class MockVisionProcessor : IVisionProcessor
    {
        private bool _isLoaded;
        private double _scoreThreshold = 0.7;
        private VisionResult _lastResult;
        private readonly Random _random;
        private readonly int _stationId;

        public bool IsLoaded => _isLoaded;

        public event EventHandler<VisionProcessCompletedEventArgs> ProcessCompleted;
        public event EventHandler<Exception> ProcessError;

        public MockVisionProcessor(int stationId)
        {
            _stationId = stationId;
            _random = new Random();
        }

        public bool LoadToolBlock(string vppPath)
        {
            try
            {
                // 模拟加载延迟
                Thread.Sleep(100);
                _isLoaded = true;
                FileLogger.Instance.Info($"模拟视觉处理器 Station{_stationId} 工具块已加载: {vppPath}", "MockVision");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"模拟视觉处理器加载失败: {ex.Message}", ex, "MockVision");
                return false;
            }
        }

        public void UnloadToolBlock()
        {
            _isLoaded = false;
            _lastResult = null;
            FileLogger.Instance.Info($"模拟视觉处理器 Station{_stationId} 工具块已卸载", "MockVision");
        }

        public VisionResult Execute(Bitmap image)
        {
            if (!_isLoaded)
            {
                ProcessError?.Invoke(this, new InvalidOperationException("工具块未加载"));
                return null;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 模拟处理延迟
                Thread.Sleep(_random.Next(30, 100));

                // 模拟匹配结果 (80%概率成功)
                var isFound = _random.NextDouble() > 0.2;
                var score = isFound ? 0.7 + _random.NextDouble() * 0.3 : _random.NextDouble() * 0.5;
                var angle = isFound ? _random.NextDouble() * 360 - 180 : 0;

                stopwatch.Stop();

                _lastResult = new VisionResult
                {
                    Found = isFound && score >= _scoreThreshold,
                    Score = score,
                    Angle = angle,
                    X = image.Width / 2.0 + (_random.NextDouble() - 0.5) * 50,
                    Y = image.Height / 2.0 + (_random.NextDouble() - 0.5) * 50,
                    ProcessTimeMs = stopwatch.ElapsedMilliseconds,
                    ResultImage = CreateResultImage(image, isFound, angle, score)
                };

                FileLogger.Instance.Debug(
                    $"模拟视觉处理 Station{_stationId}: Found={_lastResult.Found}, Score={score:F3}, Angle={angle:F2}, Time={stopwatch.ElapsedMilliseconds}ms",
                    "MockVision");

                ProcessCompleted?.Invoke(this, new VisionProcessCompletedEventArgs
                {
                    Result = _lastResult,
                    StationId = _stationId
                });

                return _lastResult;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"模拟视觉处理失败: {ex.Message}", ex, "MockVision");
                ProcessError?.Invoke(this, ex);
                return null;
            }
        }

        private Bitmap CreateResultImage(Bitmap original, bool found, double angle, double score)
        {
            var result = new Bitmap(original);
            using (var g = Graphics.FromImage(result))
            {
                var centerX = result.Width / 2;
                var centerY = result.Height / 2;

                if (found)
                {
                    // 绘制绿色十字和角度线
                    using (var pen = new Pen(Color.Lime, 2))
                    {
                        g.DrawLine(pen, centerX - 50, centerY, centerX + 50, centerY);
                        g.DrawLine(pen, centerX, centerY - 50, centerX, centerY + 50);

                        // 绘制角度线
                        var radians = angle * Math.PI / 180;
                        var endX = centerX + (int)(80 * Math.Cos(radians));
                        var endY = centerY + (int)(80 * Math.Sin(radians));
                        g.DrawLine(pen, centerX, centerY, endX, endY);
                    }

                    g.DrawString($"OK  Angle:{angle:F1}  Score:{score:F2}",
                        new Font("Arial", 14, FontStyle.Bold),
                        Brushes.Lime, 10, 10);
                }
                else
                {
                    // 绘制红色X
                    using (var pen = new Pen(Color.Red, 2))
                    {
                        g.DrawLine(pen, centerX - 50, centerY - 50, centerX + 50, centerY + 50);
                        g.DrawLine(pen, centerX - 50, centerY + 50, centerX + 50, centerY - 50);
                    }

                    g.DrawString($"NG  Score:{score:F2}",
                        new Font("Arial", 14, FontStyle.Bold),
                        Brushes.Red, 10, 10);
                }
            }
            return result;
        }

        public VisionResult GetLastResult()
        {
            return _lastResult;
        }

        public void SetScoreThreshold(double threshold)
        {
            _scoreThreshold = threshold;
            FileLogger.Instance.Debug($"模拟视觉处理器 Station{_stationId} 设置分数阈值: {threshold}", "MockVision");
        }

        public void Dispose()
        {
            UnloadToolBlock();
        }
    }
}
