using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using VisionOTA.Common.Constants;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Infrastructure.Storage
{
    /// <summary>
    /// 图片存储服务
    /// </summary>
    public class ImageStorage
    {
        private static readonly Lazy<ImageStorage> _instance =
            new Lazy<ImageStorage>(() => new ImageStorage());

        private readonly string _baseDirectory;

        public static ImageStorage Instance => _instance.Value;

        private ImageStorage()
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            var config = ConfigManager.Instance.SystemCfg;

            var imagePath = Path.Combine(_baseDirectory, config.ImageRootPath);
            var ngPath = Path.Combine(_baseDirectory, config.NgImagePath);

            if (!Directory.Exists(imagePath))
                Directory.CreateDirectory(imagePath);

            if (!Directory.Exists(ngPath))
                Directory.CreateDirectory(ngPath);

            // 创建各工位子目录
            for (int i = 1; i <= SystemConstants.StationCount; i++)
            {
                var stationPath = Path.Combine(imagePath, $"Station{i}");
                if (!Directory.Exists(stationPath))
                    Directory.CreateDirectory(stationPath);
            }
        }

        /// <summary>
        /// 保存检测图片
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="stationId">工位ID</param>
        /// <param name="isOk">是否OK</param>
        /// <returns>保存的文件路径</returns>
        public string SaveImage(Bitmap image, int stationId, bool isOk)
        {
            if (image == null)
                return null;

            try
            {
                var config = ConfigManager.Instance.SystemCfg;
                var timestamp = DateTime.Now;
                var result = isOk ? "OK" : "NG";
                var fileName = $"{timestamp:yyyyMMdd_HHmmss_fff}_Station{stationId}_{result}.bmp";

                string filePath;

                if (isOk)
                {
                    // OK图片保存到最近图片目录
                    if (!config.SaveOkImages)
                        return null;

                    var stationPath = Path.Combine(_baseDirectory, config.ImageRootPath, $"Station{stationId}");
                    filePath = Path.Combine(stationPath, fileName);

                    // 清理旧图片
                    CleanupOldImages(stationPath, config.RecentImageCount);
                }
                else
                {
                    // NG图片保存到NG目录，按日期和工位分类
                    var dateFolder = timestamp.ToString("yyyy-MM-dd");
                    var ngPath = Path.Combine(_baseDirectory, config.NgImagePath, dateFolder, $"Station{stationId}");

                    if (!Directory.Exists(ngPath))
                        Directory.CreateDirectory(ngPath);

                    filePath = Path.Combine(ngPath, fileName);
                }

                image.Save(filePath, ImageFormat.Bmp);
                FileLogger.Instance.Debug($"图片已保存: {filePath}", "ImageStorage");
                return filePath;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"保存图片失败: {ex.Message}", ex, "ImageStorage");
                return null;
            }
        }

        /// <summary>
        /// 清理旧图片，保留最近N张
        /// </summary>
        private void CleanupOldImages(string directory, int keepCount)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*.bmp")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (files.Count > keepCount)
                {
                    var filesToDelete = files.Skip(keepCount);
                    foreach (var file in filesToDelete)
                    {
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"清理旧图片失败: {ex.Message}", ex, "ImageStorage");
            }
        }

        /// <summary>
        /// 清理过期NG图片
        /// </summary>
        public void CleanupExpiredNgImages()
        {
            try
            {
                var config = ConfigManager.Instance.SystemCfg;
                var ngPath = Path.Combine(_baseDirectory, config.NgImagePath);
                var cutoffDate = DateTime.Now.AddDays(-config.NgImageRetentionDays);

                if (!Directory.Exists(ngPath))
                    return;

                var directories = Directory.GetDirectories(ngPath);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (DateTime.TryParse(dirName, out var dirDate) && dirDate < cutoffDate)
                    {
                        Directory.Delete(dir, true);
                        FileLogger.Instance.Info($"已清理过期NG图片目录: {dirName}", "ImageStorage");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"清理过期NG图片失败: {ex.Message}", ex, "ImageStorage");
            }
        }

        /// <summary>
        /// 获取磁盘剩余空间(GB)
        /// </summary>
        public double GetFreeDiskSpaceGB()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(_baseDirectory));
                return drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 检查磁盘空间是否充足
        /// </summary>
        public bool IsDiskSpaceSufficient()
        {
            var config = ConfigManager.Instance.SystemCfg;
            var freeSpace = GetFreeDiskSpaceGB();
            return freeSpace < 0 || freeSpace >= config.DiskSpaceAlarmThresholdGB;
        }
    }
}
