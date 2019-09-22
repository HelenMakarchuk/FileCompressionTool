using System.Threading;

namespace FileCompressionTool.Domain.EventArgs
{
    public class CancelEventArgs : System.EventArgs
    {
        public CancelEventArgs(CancellationToken token)
        {
            Token = token;
        }

        public CancellationToken Token { get; set; }
    }
}
