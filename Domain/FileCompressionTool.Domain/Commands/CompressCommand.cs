using Autofac;
using FileCompressionTool.Domain.CommandOptions;
using FileCompressionTool.Domain.EventArgs;
using FileCompressionTool.Domain.Exceptions;
using FileCompressionTool.Domain.Services.Compressor;
using FileCompressionTool.Domain.Services.Reader;
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
    /// Сжатие файла
    /// </summary>
    public class CompressCommand : ICommand
    {
        public const long MAX_FILE_SIZE = 34359738368; // 32 GB

        readonly CompressCommandOptions _commandOptions;
        readonly ILogger<CompressCommand> _logger;
        readonly CancellationTokenSource _tokenSource;
        readonly IReader _reader;
        readonly ICompressor _compressor;
        readonly IWriter _writer;
        readonly int _maxWorkersCount;
        Semaphore _readSemaphore;
        IWorkerFactory _workerFactory;
        bool _isDisposed = false;

        public CompressCommand(IContainer serviceContainer, CompressCommandOptions commandOptions, int maxWorkersCount)
        {
            _maxWorkersCount = maxWorkersCount;
            _commandOptions = commandOptions;
            _tokenSource = new CancellationTokenSource();
            _readSemaphore = new Semaphore(_maxWorkersCount, _maxWorkersCount);

            _logger = serviceContainer.Resolve<ILogger<CompressCommand>>();
            _workerFactory = serviceContainer.Resolve<IWorkerFactory>(new NamedParameter("maxWorkersCount", _maxWorkersCount));

            _reader = serviceContainer.Resolve<IReader>();
            _reader.BlockRead += OnBlockRead;

            _compressor = serviceContainer.Resolve<ICompressor>();
            _compressor.BlockCompressed += OnBlockCompressed;

            _writer = serviceContainer.Resolve<IWriter>();
            _writer.BlockScheduled += OnWriteBlockScheduled;
        }

        ~CompressCommand()
        {
            Dispose(false);
        }

        public void Run(CancellationToken token)
        {
            try
            {
                _logger.LogDebug($"Start compressing file {_commandOptions.SourcePath}");

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

                    long leftRead = sourceFileInfo.Length;
                    int readBlockNumber = 0;

                    while (leftRead > 0)
                    {
                        WaitHandle.WaitAny(new WaitHandle[] { _readSemaphore, token.WaitHandle, _tokenSource.Token.WaitHandle, _workerFactory.Token.WaitHandle });

                        if (token.IsCancellationRequested || _tokenSource.Token.IsCancellationRequested || _workerFactory.Token.IsCancellationRequested)
                            break;

                        RunReadBlock(readBlockNumber, ref leftRead, token);
                        readBlockNumber++;
                    }

                    if (!_workerFactory.IsEmpty())
                        _workerFactory.WaitAll(token);
                }
            }
            catch (AggregateException ex)
            {
                throw new CommandRunException(ex);
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

                    if (_reader is IDisposable)
                        ((IDisposable)_reader)?.Dispose();

                    if (_compressor is IDisposable)
                        ((IDisposable)_compressor)?.Dispose();

                    if (_writer is IDisposable)
                        ((IDisposable)_writer)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        void RunReadBlock(int readBlockNumber, ref long leftRead, CancellationToken token)
        {
            var blockSize = (int)(leftRead < Block.MAX_SIZE ? leftRead : Block.MAX_SIZE);
            var block = new Block(readBlockNumber, new FileStream(_commandOptions.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read), leftRead, blockSize);
            leftRead -= blockSize;
            block.IsLast = leftRead == 0;

            var work = new Work(() => _reader.ReadBlock(block, token));
            _workerFactory.Run(work, token);
        }

        void OnBlockRead(object sender, BlockEventArgs e)
        {
            var block = e.Block;
            var token = e.Token;
            var work = new Work(() => _compressor.Compress(block, token));
            _workerFactory.Run(work, token);
            _readSemaphore.Release();
        }

        void OnBlockCompressed(object sender, BlockEventArgs e)
        {
            _writer.ScheduleBlockWrite(e.Block, e.Token, true);
        }

        void OnWriteBlockScheduled(object sender, ScheduleWriteBlockEventArgs e)
        {
            var work = new Work(() => _writer.WriteBlock(e.Block, _commandOptions.TargetPath, e.TargetOffset, e.Token, e.PrependWithMetadata));
            _workerFactory.Run(work, e.Token, e.Block.IsLast);
        }
    }
}