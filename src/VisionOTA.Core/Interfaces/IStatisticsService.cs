using System;
using System.Collections.Generic;
using VisionOTA.Core.Models;

namespace VisionOTA.Core.Interfaces
{
    /// <summary>
    /// 统计服务接口
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// 获取工位统计数据
        /// </summary>
        /// <param name="stationId">工位ID</param>
        /// <returns>统计数据</returns>
        StationStatistics GetStationStatistics(int stationId);

        /// <summary>
        /// 添加检测结果
        /// </summary>
        /// <param name="stationId">工位ID</param>
        /// <param name="isOk">是否OK</param>
        void AddResult(int stationId, bool isOk);

        /// <summary>
        /// 清零工位统计
        /// </summary>
        /// <param name="stationId">工位ID</param>
        void ResetStation(int stationId);

        /// <summary>
        /// 清零所有统计
        /// </summary>
        void ResetAll();

        /// <summary>
        /// 保存统计数据
        /// </summary>
        void SaveStatistics();

        /// <summary>
        /// 加载统计数据
        /// </summary>
        void LoadStatistics();

        /// <summary>
        /// 导出统计报表
        /// </summary>
        /// <param name="filePath">导出路径</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        void ExportReport(string filePath, DateTime startTime, DateTime endTime);
    }
}
