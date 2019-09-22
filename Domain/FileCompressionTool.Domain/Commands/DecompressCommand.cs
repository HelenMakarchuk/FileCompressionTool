using Autofac;
using FileCompressionTool.Domain.CommandOptions;
using FileCompressionTool.Domain.EventArgs;
using FileCompressionTool.Domain.Exceptions;
using FileCompressionTool.Domain.Services.Decompressor;
using FileCompressionTool.Domain.Services.Writer;
using FileCompressionTool.Domain.Workers;
using FileCompressionTool.Domain.Works;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;

namespace FileCompressionTool.Domain.Commands
{
    /// <summary>
    /// Распаковка сжатого файла
    /// </summary>
    public class DecompressCommand : ICommand
    {
        public const long MAX_FILE_SIZE = 34359738368; // 32 GB

        readonly DecompressCommandOptions _commandOptions;
        readonly ILogger<DecompressCommand> _logger;
        readonly CancellationTokenSource _tokenSource;
        readonly IDecompressor _decompressor;
        readonly IWriter _writer;
        readonly int _maxWorkersCount;
        Semaphore _readSemaphore;
        IWorkerFactory _workerFactory;
        bool _isDisposed = false;

        public DecompressCommand(IContainer serviceContainer, DecompressCommandOptions commandOptions, int maxWorkersCount)
        {
            _maxWorkersCount = maxWorkersCount;
            _commandOptions = commandOptions;
            _tokenSource = new CancellationTokenSource();
            _readSemaphore = new Semaphore(_maxWorkersCount, _maxWorkersCount);

            _logger = serviceContainer.Resolve<ILogger<DecompressCommand>>();
            _workerFactory = serviceContainer.Resolve<IWorkerFactory>(new NamedParameter("maxWorkersCount", _maxWorkersCount));

            _decompressor = serviceContainer.Resolve<IDecompressor>();
            _decompressor.BlockDecompressed += OnBlockDecompressed;

            _writer = serviceContainer.Resolve<IWriter>();
            _writer.BlockScheduled += OnWriteBlockScheduled;
        }

        ~DecompressCommand()
        {
            Dispose(false);
        }

        public void Run(CancellationToken token)
        {
            try
            {
                _logger.LogDebug($"Start decompressing file {_commandOptions.SourcePath}");

                var sourceFileInfo = new FileInfo(_commandOptions.SourcePath);

                if (!sourceFileInfo.Exists)
                {
                    throw new FileNotFoundException("Source file doesn't exist", _commandOptions.SourcePath);
                }
                else if (sourceFileInfo.Length > MAX_FILE_SIZE)
                {
                    throw new NotSupportedException("Source file is too big");
                }
                else
                {
                    File.Create(_commandOptions.TargetPath).Dispose();

                    using (var sourceStream = new FileStream(_commandOptions.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long leftRead = sourceStream.Length;
                        int readBlockNumber = 0;

                        while (leftRead > 0)
                        {
                            WaitHandle.WaitAny(new WaitHandle[] { _readSemaphore, token.WaitHandle, _tokenSource.Token.WaitHandle, _workerFactory.Token.WaitHandle });

                            if (token.IsCancellationRequested || _tokenSource.Token.IsCancellationRequested || _workerFactory.Token.IsCancellationRequested)
                                break;

                            RunInitialWork(readBlockNumber, ref leftRead, sourceStream, token);
                            readBlockNumber++;
                        }
                    }

                    if (!_workerFactory.IsEmpty())
                        _workerFactory.WaitAll(token);
                }
            }
            catch (Exception ex)
            {
                throw new CommandRunException(ex.Message, ex.InnerException);
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
                    if (_workerFactory is IDisposable)
                        ((IDisposable)_workerFactory)?.Dispose();

                    if (_decompressor is IDisposable)
                        ((IDisposable)_decompressor)?.Dispose();

                    if (_writer is IDisposable)
                        ((IDisposable)_writer)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        void RunInitialWork(int readBlockNumber, ref long leftRead, Stream sourceStream, CancellationToken token)
        {
            sourceStream.Seek(-leftRead, SeekOrigin.End);

            var block = Block.Parse(readBlockNumber, new FileStream(_commandOptions.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read), leftRead);
            leftRead -= Block.METADATA_LENGTH + block.Size;
            block.IsLast = leftRead == 0;

            if (block.IsCompressed)
            {
                var work = new Work(() => _decompressor.Decompress(block, token));
                _workerFactory.Run(work, token);
            }
            else
            {
                var work = new Work(() => _writer.ScheduleBlockWrite(block, token));
                _workerFactory.Run(work, token);
                _readSemaphore.Release();
            }
        }

        void OnBlockDecompressed(object sender, BlockEventArgs e)
        {
            _writer.ScheduleBlockWrite(e.Block, e.Token);
            _readSemaphore.Release();
        }

        void OnWriteBlockScheduled(object sender, ScheduleWriteBlockEventArgs e)
        {
            var work = new Work(() => _writer.WriteBlock(e.Block, _commandOptions.TargetPath, e.TargetOffset, e.Token, e.PrependWithMetadata));
            _workerFactory.Run(work, e.Token, e.Block.IsLast);
        }
    }
}