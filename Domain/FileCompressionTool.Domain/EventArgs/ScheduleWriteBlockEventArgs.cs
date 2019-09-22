using System.Threading;

namespace FileCompressionTool.Domain.EventArgs
{
    public class ScheduleWriteBlockEventArgs : BlockEventArgs
    {
        public ScheduleWriteBlockEventArgs(long targetOffset, Block block, CancellationToken token, bool prependWithMetadata = false)
            : base(block, token)
        {
            TargetOffset = targetOffset;
            PrependWithMetadata = prependWithMetadata;
        }

        public long TargetOffset { get; private set; }
        public bool PrependWithMetadata { get; private set; }
    }
}