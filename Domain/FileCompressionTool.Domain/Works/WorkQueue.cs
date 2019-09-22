using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FileCompressionTool.Domain.Works
{
    /// <summary>
    /// Очередь работ
    /// </summary>
    public class WorkQueue : IWorkQueue, IDisposable
    {
        readonly ILogger<WorkQueue> _logger;
        readonly object _lockObj;
        Queue<IWork> _works;
        ManualResetEvent _workAdded;
        bool _isDisposed = false;

        public WorkQueue(ILogger<WorkQueue> logger)
        {
            _logger = logger;
            _works = new Queue<IWork>();
            _lockObj = new object();
            _workAdded = new ManualResetEvent(false);
        }

        ~WorkQueue()
        {
            Dispose(false);
        }

        public ManualResetEvent WorkAssignmentCompleted { get; set; }

        /// <summary>
        /// Добавление работы в конец очереди
        /// </summary>
        /// <param name="work">работа</param>
        /// <param name="token">токен отмены операции</param>
        public void Enqueue(IWork work, CancellationToken token, bool isLast = false)
        {
            if (!token.IsCancellationRequested)
            {
                try
                {
                    Monitor.Enter(_lockObj);

                    _works.Enqueue(work);
                    _workAdded.Set();
                    _logger.LogDebug("A new work has been added to the end of the queue");
                }
                finally
                {
                    Monitor.Exit(_lockObj);
                }
            }
        }

        /// <summary>
        /// Удаление и возврат работы из начала очереди
        /// </summary>
        /// <param name="work">работа из начала очереди в случае успеха, иначе NULL</param>
        /// <param name="workAssignmentCompleted">признак того, что добавление новых работ в очередь работ завершено</param>
        /// <param name="token">токен отмены операции</param>
        /// <returns>TRUE в случае успеха, иначе FALSE</returns>
        public bool TryDequeue(out IWork work, ManualResetEvent workAssignmentCompleted, CancellationToken token)
        {
            work = null;

            while (work == null)
            {
                var waitedEventIndex = WaitHandle.WaitAny(new WaitHandle[] { _workAdded, workAssignmentCompleted, token.WaitHandle });

                if (token.IsCancellationRequested)
                    return false;

                try
                {
                    Monitor.Enter(_lockObj);

                    if (_works.Any())
                    {
                        work = _works.Dequeue();
                        _logger.LogDebug("The work has been removed from the beginning of the queue");

                        if (!_works.Any())
                            _workAdded.Reset();

                        return true;
                    }
                    else if (waitedEventIndex == 1)
                    {
                        return false;
                    }
                }
                finally
                {
                    Monitor.Exit(_lockObj);
                }
            }

            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                if (disposeManagedResources)
                {
                    _works = null;
                    _workAdded.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}