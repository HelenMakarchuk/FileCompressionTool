using FileCompressionTool.Domain.EventArgs;
using FileCompressionTool.Domain.Exceptions;
using FileCompressionTool.Domain.Works;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Workers
{
    /// <summary>
    /// Работник, выполняющий работу в отдельном потоке
    /// </summary>
    public class Worker : IWorker
    {
        readonly object _lockObj;
        Thread _thread;
        bool _isDisposed = false;
        bool _isStarted;
        ManualResetEvent _waited;
        IWork _work;

        public Worker(IWork work, CancellationToken token)
        {
            _lockObj = new object();
            _waited = new ManualResetEvent(false);

            try
            {
                Monitor.Enter(_lockObj);

                _work = work;
                _thread = new Thread(() => Run(token));
                _thread.IsBackground = true;
                OnCreated();
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        ~Worker()
        {
            Dispose(false);
        }

        public event EventHandler WaitingForWork;
        public event EventHandler<FaultedEventArgs> Faulted;

        public WorkerStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        public void Wait(CancellationToken token)
        {
            WaitHandle.WaitAny(new WaitHandle[] { _waited, token.WaitHandle });
        }

        public void Start(CancellationToken token)
        {
            if (_isStarted)
                throw new WorkerStateException("The worker has already been started.");

            try
            {
                Monitor.Enter(_lockObj);

                if (!token.IsCancellationRequested)
                {
                    OnStarting();
                    _thread.Start();
                }
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        public void ContinueWith(IWork work, CancellationToken token)
        {
            if (!_isStarted)
                throw new WorkerStateException("The worker must be already started and not yet stopped to running.");

            if (!token.IsCancellationRequested)
                _work = work;
        }

        public void Complete(CancellationToken token)
        {
            OnCompleted();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Run(CancellationToken token)
        {
            while (_work != null)
            {
                try
                {
                    if (!token.IsCancellationRequested)
                    {
                        OnRunning();
                        _work?.Run();
                    }
                }
                catch (OperationCanceledException)
                {
                    OnCanceled();
                }
                catch (Exception ex)
                {
                    OnFaulted(new FaultedEventArgs(ex, token));
                }
                finally
                {
                    if (_work is IDisposable)
                        ((IDisposable)_work)?.Dispose();

                    _work = null;
                }

                OnWaitingForWork(null);
            }
        }

        void Stop()
        {
            try
            {
                Monitor.Enter(_lockObj);

                if (_isStarted)
                {
                    _thread?.Join();
                    OnStopped();
                }
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        void OnCreated()
        {
            Status = WorkerStatus.Created;
        }

        void OnStarting()
        {
            _isStarted = true;
        }

        void OnRunning()
        {
            Status = WorkerStatus.Running;
        }

        void OnWaitingForWork(CancelEventArgs e)
        {
            try
            {
                Status = WorkerStatus.WaitingForWork;

                WaitingForWork?.Invoke(this, e);
            }
            finally
            {
                e = null;
            }
        }

        void OnFaulted(FaultedEventArgs e)
        {
            try
            {
                Status = WorkerStatus.Faulted;
                Exception = e.Exception;

                Faulted?.Invoke(this, e);
                _waited.Set();
            }
            finally
            {
                e = null;
            }
        }

        void OnCompleted()
        {
            Status = WorkerStatus.Completed;
            _waited.Set();
        }

        void OnCanceled()
        {
            Status = WorkerStatus.Canceled;
            _waited.Set();
        }

        void OnStopped()
        {
            Status = WorkerStatus.Stopped;
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                if (disposeManagedResources)
                {
                    Faulted = null;
                    WaitingForWork = null;
                    Stop();
                    _thread = null;
                    _waited.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}