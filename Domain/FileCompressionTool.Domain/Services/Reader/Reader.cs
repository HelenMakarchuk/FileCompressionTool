using FileCompressionTool.Domain.EventArgs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Reader
{
    public class Reader : IReader, IDisposable
    {
        readonly CancellationTokenSource _tokenSource;
        readonly ILogger<Reader> _logger;
        bool _isDisposed = false;

        public Reader(ILogger<Reader> logger)
        {
            _logger = logger;
            _tokenSource = new CancellationTokenSource();
        }

        ~Reader()
        {
            Dispose(false);
        }

        public event EventHandler<BlockEventArgs> BlockRead;

        /// <summary>
        /// Чтение блока исходного файла
        /// </summary>
        /// <param name="block">блок для чтения</param>
        /// <param name="token">токен отмены операции чтения</param>
        public void ReadBlock(Block block, CancellationToken token)
        {
            var memoryStream = new MemoryStream();
            var bufferSize = 8192; // 8 KB
            var buffer = new byte[bufferSize];
            var leftRead = block.Size;

            try
            {
                block.Stream.Seek(-block.EndOffset, SeekOrigin.End);

                while (leftRead > 0)
                {
                    token.ThrowIfCancellationRequested();

                    if (leftRead < bufferSize)
                        bufferSize = leftRead;

                    var read = block.Stream.Read(buffer, 0, bufferSize);
                    memoryStream.Write(buffer, 0, read);
                    leftRead -= read;
                }

                block.Stream.Dispose();

                block.Stream = memoryStream;
                block.Size = (int)memoryStream.Length;
                block.EndOffset = memoryStream.Length;

                OnBlockRead(new BlockEventArgs(block, token));
            }
            catch (Exception)
            {
                memoryStream.Dispose();
                block.Dispose();

                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void OnBlockRead(BlockEventArgs e)
        {
            try
            {
                BlockRead?.Invoke(this, e);
            }
            finally
            {
                e = null;
            }
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                _tokenSource.Cancel();

                if (disposeManagedResources)
                {
                    foreach (EventHandler<BlockEventArgs> eventDelegate in BlockRead.GetInvocationList())
                        BlockRead -= eventDelegate;
                }

                _isDisposed = true;
            }
        }
    }
}