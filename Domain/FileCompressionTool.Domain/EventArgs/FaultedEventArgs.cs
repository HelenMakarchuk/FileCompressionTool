using System;
using System.Threading;

namespace FileCompressionTool.Domain.EventArgs
{
    public class FaultedEventArgs : CancelEventArgs
    {
        public FaultedEventArgs(Exception ex, CancellationToken token)
            : base(token)
        {
            Exception = ex;
        }

        public Exception Exception { get; set; }
    }
}
