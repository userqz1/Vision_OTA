using System;
using System.Windows.Input;

namespace VisionOTA.Common.Mvvm
{
    /// <summary>
    /// 命令工厂 - 简化命令创建
    /// </summary>
    public static class CommandFactory
    {
        /// <summary>
        /// 创建命令
        /// </summary>
        public static ICommand Create(Action execute, Func<bool> canExecute = null)
        {
            return new RelayCommand(
                _ => execute(),
                canExecute == null ? (Predicate<object>)null : _ => canExecute()
            );
        }

        /// <summary>
        /// 创建带参数的命令
        /// </summary>
        public static ICommand Create<T>(Action<T> execute, Predicate<T> canExecute = null)
        {
            return new RelayCommand(
                p => execute((T)p),
                canExecute == null ? (Predicate<object>)null : p => p is T t && canExecute(t)
            );
        }

        /// <summary>
        /// 创建异步命令
        /// </summary>
        public static ICommand CreateAsync(Func<System.Threading.Tasks.Task> execute, Func<bool> canExecute = null)
        {
            return new RelayCommand(
                async _ => await execute(),
                canExecute == null ? (Predicate<object>)null : _ => canExecute()
            );
        }
    }
}
