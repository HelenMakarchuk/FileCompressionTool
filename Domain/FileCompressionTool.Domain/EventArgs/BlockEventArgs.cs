using System.Threading;

namespace FileCompressionTool.Domain.EventArgs
{
    public class BlockEventArgs : CancelEventArgs
    {
        public BlockEventArgs(Block block, CancellationToken token)
            : base(token)
        {
            Block = block;
        }

        public Block Block { get; private set; }
    }
}