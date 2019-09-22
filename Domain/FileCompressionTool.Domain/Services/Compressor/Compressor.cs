using FileCompressionTool.Domain.EventArgs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Compressor
{
    public class Compressor : ICompressor, IDisposable
    {
        readonly CancellationTokenSource _tokenSource;
        readonly ILogger<Compressor> _logger;
        bool _isDisposed = false;

        public Compressor(ILogger<Compressor> logger)
        {
            _logger = logger;
            _tokenSource = new CancellationTokenSource();
        }

        ~Compressor()
        {
            Dispose(false);
        }

        public event EventHandler<BlockEventArgs> BlockCompressed;

        /// <summary>
        /// Cжатие блока исходного файла
        /// </summary>
        /// <param name="block">блок для сжатия</param>
        /// <param name="token">токен отмены операции сжатия</param>
        public void Compress(Block block, CancellationToken token)
        {
            var compressedStream = new MemoryStream();

            try
            {
                token.ThrowIfCancellationRequested();
                block.Stream.Seek(-block.EndOffset, SeekOrigin.End);

                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                {
                    var buffer = new byte[block.Size];
                    var read = block.Stream.Read(buffer, 0, buffer.Length);
                    zipStream.Write(buffer, 0, read);
                }

                if (compressedStream.Length < block.Stream.Length)
                {
                    _logger.LogDebug($"Block with number {block.Number} was compressed from {block.Size} to {compressedStream.Length} bytes");

                    block.Stream.Dispose();

                    block.Stream = compressedStream;
                    block.Size = (int)compressedStream.Length;
                    block.EndOffset = compressedStream.Length;
                    block.IsCompressed = true;
                }
                else
                {
                    compressedStream.Dispose();
                }

                OnBlockCompressed(new BlockEventArgs(block, token));
            }
            catch (Exception)
            {
                block.Dispose();
                compressedStream.Dispose();

                throw;
            }
        }

        void OnBlockCompressed(BlockEventArgs e)
        {
            try
            {
                BlockCompressed?.Invoke(this, e);
            }
            finally
            {
                e = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!_isDisposed)
            {
                _tokenSource?.Cancel();

                if (disposeManagedResources)
                {
                    foreach (EventHandler<BlockEventArgs> eventDelegate in BlockCompressed?.GetInvocationList())
                        BlockCompressed -= eventDelegate;
                }

                _isDisposed = true;
            }
        }
    }
}