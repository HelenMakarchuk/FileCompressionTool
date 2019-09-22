using System;
using System.Threading;

namespace FileCompressionTool.Domain.Commands
{
    public interface ICommand : IDisposable
    {
        void Run(CancellationToken token);
    }
}