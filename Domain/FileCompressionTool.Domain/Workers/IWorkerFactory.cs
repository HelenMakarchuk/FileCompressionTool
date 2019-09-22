using FileCompressionTool.Domain.Works;
using System.Threading;

namespace FileCompressionTool.Domain.Workers
{
    public interface IWorkerFactory
    {
        CancellationToken Token { get; }
        bool IsEmpty();
        void Run(IWork work, CancellationToken token, bool isLast = false);
        void WaitAll(CancellationToken token);
    }
}