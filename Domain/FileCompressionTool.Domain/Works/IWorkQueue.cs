using System.Threading;

namespace FileCompressionTool.Domain.Works
{
    public interface IWorkQueue
    {
        void Enqueue(IWork work, CancellationToken token, bool isLast = false);
        bool TryDequeue(out IWork work, ManualResetEvent _workAssignmentCompleted, CancellationToken token);
    }
}