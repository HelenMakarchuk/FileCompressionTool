using FileCompressionTool.Domain.EventArgs;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Compressor
{
    public interface ICompressor
    {
        event EventHandler<BlockEventArgs> BlockCompressed;
        void Compress(Block block, CancellationToken token);
    }
}