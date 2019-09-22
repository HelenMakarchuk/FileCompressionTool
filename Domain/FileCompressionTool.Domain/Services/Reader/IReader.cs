using FileCompressionTool.Domain.EventArgs;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Reader
{
    public interface IReader
    {
        event EventHandler<BlockEventArgs> BlockRead;
        void ReadBlock(Block block, CancellationToken token);
    }
}
