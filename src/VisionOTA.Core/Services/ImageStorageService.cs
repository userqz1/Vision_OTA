using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Storage;

namespace VisionOTA.Core.Services
{
    /// <summary>
    /// 图片存储服务（Core层封装）
    /// </summary>
    public class ImageStorageService
    {
        private readonly ImageStorage _imageStorage;

        public ImageStorageService()
        {
            _imageStorage = ImageStorage.Instance;
        }

        /// <summary>
        /// 保存检测图片
        /// </summary>
        public string SaveInspectionImage(Bitmap image, int stationId, bool isOk)
        {
            return _imageStorage.SaveImage(image, stationId, isOk);
        }

        /// <summary>
        /// 加载图片
        /// </summary>
        public Bitmap LoadImage(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                return new Bitmap(filePath);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"加载图片失败: {ex.Message}", ex, "ImageStorage");
                return null;
            }
        }

        /// <summary>
        /// 清理过期图片
        /// </summary>
        public void CleanupExpiredImages()
        {
            _imageStorage.CleanupExpiredNgImages();
        }

        /// <summary>
        /// 检查磁盘空间
        /// </summary>
        public bool CheckDiskSpace()
        {
            return _imageStorage.IsDiskSpaceSufficient();
        }

        /// <summary>
        /// 获取磁盘剩余空间(GB)
        /// </summary>
        public double GetFreeDiskSpaceGB()
        {
            return _imageStorage.GetFreeDiskSpaceGB();
        }
    }
}
