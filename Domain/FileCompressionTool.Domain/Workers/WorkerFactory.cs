using Autofac;
using FileCompressionTool.Domain.EventArgs;
using FileCompressionTool.Domain.Works;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FileCompressionTool.Domain.Workers
{
    /// <summary>
    /// Менеджер работников
    /// </summary>
    public class WorkerFactory : IWorkerFactory, IDisposable
    {
        readonly int _maxWorkersCount;
        readonly object _lockObj;
        readonly ILogger<WorkerFactory> _logger;
        readonly CancellationTokenSource _tokenSource;
        List<IWorker> _workers;
        bool _isDisposed = false;
        IWorkQueue _workQueue;
        ManualResetEvent _workAssignmentCompleted;

        public WorkerFactory(IContainer serviceContainer, int maxWorkersCount)
        {
            _logger = serviceContainer.Resolve<ILogger<WorkerFactory>>();
            _workQueue = serviceContainer.Resolve<IWorkQueue>();
            _maxWorkersCount = maxWorkersCount;
            _tokenSource = new CancellationTokenSource();
            _lockObj = new object();
            _workAssignmentCompleted = new ManualResetEvent(false);
            _workers = new List<IWorker>();
        }

        ~WorkerFactory()
        {
            Dispose(false);
        }

        public CancellationToken Token { get; private set; }

        public bool IsEmpty()
        {
            try
            {
                Monitor.Enter(_lockObj);

                return !_workers.Any();
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        /// <summary>
        /// Запуск работы на выполнение путем создания нового работника, либо добавления работы в очередь работ
        /// </summary>
        /// <param name="work">работа</param>
        /// <param name="token">токен отмены операции запуска работы на выполнение</param>
        /// <param name="isLastWork">признак того, что после текущей работы новые работы добавлены не будут</param>
        public void Run(IWork work, CancellationToken token, bool isLast = false)
        {
            if (AnyWithStatus(WorkerStatus.WaitingForWork)
                || !TryStartNewWorker(work, out var worker, token))
            {
                _workQueue.Enqueue(work, token);
            }

            if (isLast)
                _workAssignmentCompleted.Set();
        }

        /// <summary>
        /// Ожидание выполнения работ всеми рабочими потоками
        /// </summary>
        public void WaitAll(CancellationToken token)
        {
            _logger.LogTrace("Start waiting for all workers completion");

            WaitHandle.WaitAny(new WaitHandle[] { _workAssignmentCompleted, token.WaitHandle, _tokenSource.Token.WaitHandle });

            try
            {
                Monitor.Enter(_lockObj);

                _workers.ForEach(worker => worker.Wait(token));
                var errors = _workers.Where(w => w.Status == WorkerStatus.Faulted).Select(w => w.Exception);

                if (errors.Any())
                    throw new AggregateException(errors);
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }

            _logger.LogTrace("Complete waiting for all workers completion");
        }

        /// <summary>
        /// Освобождение ресурсов и остановка рабочих потоков
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Создание и запуск нового работника
        /// </summary>
        /// <param name="work">работа для работника</param>
        /// <param name="worker">созданный и запущенный работник в случае успеха, иначе NULL</param>
        /// <returns>TRUE в случае успеха, иначе FALSE</returns>
        bool TryStartNewWorker(IWork work, out Worker worker, CancellationToken token)
        {
            worker = null;

            try
            {
                Monitor.Enter(_lockObj);

                if (!token.IsCancellationRequested && !_tokenSource.Token.IsCancellationRequested && _workers.Count() < _maxWorkersCount)
                {
                    worker = new Worker(work, token);
                    _workers.Add(worker);
                    _logger.LogDebug("A new worker has been added");
                }
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }

            if (worker != null)
            {
                worker.Faulted += OnWorkerFaulted;
                worker.WaitingForWork += OnWorkerWaitingForWork;
                worker.Start(token);

                return true;
            }

            return false;
        }

        bool AnyWithStatus(WorkerStatus status)
        {
            try
            {
                Monitor.Enter(_lockObj);

                return _workers.Any(w => w.Status == status);
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        void OnWorkerWaitingForWork(object sender, System.EventArgs e)
        {
            if (_workQueue.TryDequeue(out var nextWork, _workAssignmentCompleted, _tokenSource.Token))
            {
                ((IWorker)sender).ContinueWith(nextWork, _tokenSource.Token);
            }
            else
            {
                ((IWorker)sender).Complete(_tokenSource.Token);
            }
        }

        void OnWorkerFaulted(object sender, FaultedEventArgs e)
        {
            try
            {
                _tokenSource.Cancel();
            }
            finally
            {
                e = null;
            }
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                _tokenSource.Cancel();

                if (disposeManagedResources)
                {
                    _workers.ForEach(worker => worker.Wait(_tokenSource.Token));
                    _workers?.ForEach(worker => worker.Dispose());
                    _workers = null;

                    if (_workQueue is IDisposable)
                        ((IDisposable)_workQueue)?.Dispose();

                    _workAssignmentCompleted.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}