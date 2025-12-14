using System;
using System.Collections.Generic;

namespace VisionOTA.Common.Events
{
    /// <summary>
    /// 事件聚合器，用于模块间解耦通信
    /// </summary>
    public class EventAggregator
    {
        private static readonly Lazy<EventAggregator> _instance =
            new Lazy<EventAggregator>(() => new EventAggregator());

        private readonly Dictionary<Type, List<Delegate>> _subscribers =
            new Dictionary<Type, List<Delegate>>();

        private readonly object _lock = new object();

        public static EventAggregator Instance => _instance.Value;

        private EventAggregator() { }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType] = new List<Delegate>();
                }
                _subscribers[eventType].Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType].Remove(handler);
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public void Publish<TEvent>(TEvent eventData)
        {
            List<Delegate> handlers;
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    return;

                handlers = new List<Delegate>(_subscribers[eventType]);
            }

            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<TEvent>)handler)(eventData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Event handler error: {ex.Message}");
                }
            }
        }
    }

    #region 系统事件定义

    /// <summary>
    /// 系统状态变更事件
    /// </summary>
    public class SystemStateChangedEvent
    {
        public string OldState { get; set; }
        public string NewState { get; set; }
    }

    /// <summary>
    /// 检测完成事件
    /// </summary>
    public class InspectionCompletedEvent
    {
        public int StationId { get; set; }
        public bool IsOk { get; set; }
        public double Angle { get; set; }
        public double Score { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 图像接收事件
    /// </summary>
    public class ImageReceivedEvent
    {
        public int StationId { get; set; }
        public byte[] ImageData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public class ConnectionChangedEvent
    {
        public string DeviceType { get; set; }
        public string DeviceName { get; set; }
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// 日志消息事件
    /// </summary>
    public class LogMessageEvent
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// 用户登录事件
    /// </summary>
    public class UserLoginEvent
    {
        public string Username { get; set; }
        public int PermissionLevel { get; set; }
        public DateTime LoginTime { get; set; }
    }

    #endregion
}
