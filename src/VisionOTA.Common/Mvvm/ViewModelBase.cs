using System;
using System.Windows;

namespace VisionOTA.Common.Mvvm
{
    /// <summary>
    /// ViewModel基类
    /// </summary>
    public abstract class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _isDisposed;
        private bool _isBusy;
        private string _title;

        /// <summary>
        /// 是否正在忙碌
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// 视图标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 初始化ViewModel
        /// </summary>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public virtual void Cleanup()
        {
        }

        /// <summary>
        /// 在UI线程上执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        protected void RunOnUIThread(Action action)
        {
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(action);
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 异步在UI线程上执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        protected void BeginRunOnUIThread(Action action)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                Cleanup();
            }

            _isDisposed = true;
        }

        ~ViewModelBase()
        {
            Dispose(false);
        }
    }
}
