using FileCompressionTool.Domain.EventArgs;
using FileCompressionTool.Domain.Works;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Workers
{
    public interface IWorker : IDisposable
    {
        event EventHandler WaitingForWork;
        event EventHandler<FaultedEventArgs> Faulted;

        WorkerStatus Status { get; }
        Exception Exception { get; }

        void Start(CancellationToken token);
        void ContinueWith(IWork work, CancellationToken token);
        void Wait(CancellationToken token);
        void Complete(CancellationToken token);
    }
}