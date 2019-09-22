using FileCompressionTool.Domain.EventArgs;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Decompressor
{
    public interface IDecompressor
    {
        event EventHandler<BlockEventArgs> BlockDecompressed;
        void Decompress(Block block, CancellationToken token);
    }
}