using FileCompressionTool.Domain.EventArgs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Decompressor
{
    public class Decompressor : IDecompressor, IDisposable
    {
        readonly CancellationTokenSource _tokenSource;
        readonly ILogger<Decompressor> _logger;
        bool _isDisposed = false;

        public Decompressor(ILogger<Decompressor> logger)
        {
            _logger = logger;
            _tokenSource = new CancellationTokenSource();
        }

        ~Decompressor()
        {
            Dispose(false);
        }

        public event EventHandler<BlockEventArgs> BlockDecompressed;

        /// <summary>
        /// Распаковка блока исходного файла
        /// </summary>
        /// <param name="block">блок для распаковки</param>
        /// <param name="token">токен отмены операции распаковки</param>
        public void Decompress(Block block, CancellationToken token)
        {
            var decompressedStream = new MemoryStream();

            try
            {
                token.ThrowIfCancellationRequested();
                block.Stream.Seek(-block.EndOffset, SeekOrigin.End);

                using (var zipStream = new GZipStream(block.Stream, CompressionMode.Decompress))
                {
                    var buffer = new byte[Block.MAX_SIZE];
                    var read = zipStream.Read(buffer, 0, buffer.Length);
                    decompressedStream.Write(buffer, 0, read);

                    _logger.LogDebug($"Block with number {block.Number} was decompressed from {block.Size} to {decompressedStream.Length} bytes");
                }

                block.Stream = decompressedStream;
                block.EndOffset = decompressedStream.Length;
                block.Size = (int)decompressedStream.Length;
                block.IsCompressed = false;

                OnBlockDecompressed(new BlockEventArgs(block, token));
            }
            catch (Exception)
            {
                decompressedStream.Dispose();
                block.Dispose();

                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void OnBlockDecompressed(BlockEventArgs e)
        {
            try
            {
                BlockDecompressed?.Invoke(this, e);
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
                _tokenSource?.Cancel();

                if (disposeManagedResources)
                {
                    foreach (EventHandler<BlockEventArgs> eventDelegate in BlockDecompressed?.GetInvocationList())
                        BlockDecompressed -= eventDelegate;
                }

                _isDisposed = true;
            }
        }
    }
}