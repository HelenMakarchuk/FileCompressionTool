using System.Threading;

namespace FileCompressionTool.Application
{
    interface IApp
    {
        void Run(CancellationToken token);
    }
}
