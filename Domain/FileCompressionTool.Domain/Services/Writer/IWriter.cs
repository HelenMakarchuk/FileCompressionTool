using FileCompressionTool.Domain.EventArgs;
using System;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Writer
{
    public interface IWriter
    {
        event EventHandler<ScheduleWriteBlockEventArgs> BlockScheduled;
        void ScheduleBlockWrite(Block block, CancellationToken token, bool prependWithMetadata = false);
        void WriteBlock(Block block, string targetPath, long targetOffset, CancellationToken token, bool prependWithMetadata = false);
    }
}