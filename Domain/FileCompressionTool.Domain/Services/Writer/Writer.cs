using FileCompressionTool.Domain.EventArgs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FileCompressionTool.Domain.Services.Writer
{
    public class Writer : IWriter, IDisposable
    {
        readonly CancellationTokenSource _tokenSource;
        readonly ILogger<Writer> _logger;
        readonly object _lockObj;
        Dictionary<int, Block> _waitingBlocks;
        int _nextBlockNumber;
        long _targetOffset;
        bool _isDisposed = false;

        public Writer(ILogger<Writer> logger)
        {
            _logger = logger;
            _lockObj = new object();
            _waitingBlocks = new Dictionary<int, Block>();
            _tokenSource = new CancellationTokenSource();
        }

        ~Writer()
        {
            Dispose(false);
        }

        public event EventHandler<ScheduleWriteBlockEventArgs> BlockScheduled;

        /// <summary>
        /// Планирование операции записи
        /// </summary>
        /// <param name="block">блок для записи</param>
        /// <param name="token">токен отмены операции планирования</param>
        public void ScheduleBlockWrite(Block block, CancellationToken token, bool prependWithMetadata = false)
        {
            try
            {
                Monitor.Enter(_lockObj);

                _waitingBlocks.Add(block.Number, block);

                while (_waitingBlocks.ContainsKey(_nextBlockNumber))
                {
                    token.ThrowIfCancellationRequested();

                    var nextBlock = _waitingBlocks[_nextBlockNumber];
                    OnBlockScheduled(new ScheduleWriteBlockEventArgs(_targetOffset, nextBlock, token, prependWithMetadata));
                    _targetOffset += prependWithMetadata ? Block.METADATA_LENGTH + nextBlock.Size : nextBlock.Size;
                    _waitingBlocks.Remove(_nextBlockNumber);
                    _nextBlockNumber++;
                }
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }

        /// <summary>
        /// Запись блока в конечный файл
        /// </summary>
        /// <param name="block">блок, который будет записан</param>
        /// <param name="targetPath">путь конечного файла</param>
        /// <param name="targetOffset">смещение конечного файла</param>
        /// <param name="token">токен отмены операции</param>
        /// <param name="prependWithMetadata">признак добавления метаданных блока перед блоком</param>
        public void WriteBlock(Block block, string targetPath, long targetOffset, CancellationToken token, bool prependWithMetadata = false)
        {
            try
            {
                _logger.LogTrace($"Start write block with number {block.Number}: target offset = {targetOffset}, block size = {block.Size}");

                using (block.Stream)
                using (var targetStream = new FileStream(targetPath, FileMode.Open, FileAccess.Write, FileShare.Write))
                {
                    block.Stream.Seek(-block.EndOffset, SeekOrigin.End);
                    targetStream.Seek(targetOffset, SeekOrigin.Begin);

                    if (prependWithMetadata)
                    {
                        token.ThrowIfCancellationRequested();

                        _logger.LogTrace($"Start writing metadata for block with number {block.Number}");

                        var metadata = block.GetMetadata();
                        targetStream.Write(metadata, 0, metadata.Length);

                        _logger.LogTrace($"Complete writing metadata for block with number {block.Number}");
                    }

                    var bufferSize = 8192; // 8 KB
                    var buffer = new byte[bufferSize];
                    var leftRead = block.Size;

                    while (leftRead > 0)
                    {
                        token.ThrowIfCancellationRequested();

                        if (leftRead < bufferSize)
                            bufferSize = leftRead;

                        var read = block.Stream.Read(buffer, 0, bufferSize);
                        targetStream.Write(buffer, 0, read);
                        leftRead -= read;
                    }
                }

                _logger.LogTrace($"Complete write block with number {block.Number}");
            }
            finally
            {
                block = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void OnBlockScheduled(ScheduleWriteBlockEventArgs e)
        {
            _logger.LogTrace($"block with number {e.Block.Number} has been scheduled");

            try
            {
                BlockScheduled?.Invoke(this, e);
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
                    foreach (EventHandler<ScheduleWriteBlockEventArgs> eventDelegate in BlockScheduled.GetInvocationList())
                        BlockScheduled -= eventDelegate;

                    foreach (var block in _waitingBlocks.Values)
                        block.Dispose();

                    _waitingBlocks = null;
                }

                _isDisposed = true;
            }
        }
    }
}