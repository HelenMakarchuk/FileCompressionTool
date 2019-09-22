using Autofac;
using CommandLine;
using FileCompressionTool.Domain.CommandOptions;
using FileCompressionTool.Domain.Commands;
using FileCompressionTool.Domain.Services.Compressor;
using FileCompressionTool.Domain.Services.Decompressor;
using FileCompressionTool.Domain.Services.Reader;
using FileCompressionTool.Domain.Services.Writer;
using FileCompressionTool.Domain.Workers;
using FileCompressionTool.Domain.Works;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Threading;

namespace FileCompressionTool.Application
{
    class Program
    {
        static ILogger<Program> _logger;
        static IContainer _serviceContainer;
        static CancellationTokenSource _tokenSource;
        static IApp _app;
        static bool _isDisposed;

        static int Main(string[] args)
        {
            try
            {
                ConfigureDependencies();

                var loggerFactory = _serviceContainer.Resolve<ILoggerFactory>();
                loggerFactory.AddNLog();

                _logger = _serviceContainer.Resolve<ILogger<Program>>();
                _tokenSource = new CancellationTokenSource();

                Console.CancelKeyPress += new ConsoleCancelEventHandler((sender, eventArgs) => Cancel());

                Parser.Default
                    .ParseArguments<CompressCommandOptions, DecompressCommandOptions>(args)
                    .WithParsed<ICommandOptions>(cmdOptions =>
                    {
                        _app = _serviceContainer.Resolve<IApp>(new TypedParameter(typeof(ICommandOptions), cmdOptions));

                        _logger.LogInformation("Press CTRL+C to cancel operation");
                        _app.Run(_tokenSource.Token);
                    });
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex.ToString());
                _logger.LogError(ex.Message);

                Cancel();
            }
            finally
            {
                Dispose();
            }

            return 0;
        }

        static void ConfigureDependencies()
        {
            var builder = new ContainerBuilder();

            builder
                .Register((c, p) => new App(_serviceContainer, p.TypedAs<ICommandOptions>()))
                .As<IApp>();

            builder
              .Register((c, p) => new CompressCommand(_serviceContainer, p.TypedAs<CompressCommandOptions>(), p.Named<int>("maxWorkersCount")))
              .Keyed<ICommand>(typeof(CompressCommandOptions));

            builder
              .Register((c, p) => new DecompressCommand(_serviceContainer, p.TypedAs<DecompressCommandOptions>(), p.Named<int>("maxWorkersCount")))
              .Keyed<ICommand>(typeof(DecompressCommandOptions));

            builder
                .Register((c, p) => new WorkerFactory(_serviceContainer, p.Named<int>("maxWorkersCount")))
                .As<IWorkerFactory>();

            builder.RegisterType<WorkQueue>().As<IWorkQueue>();
            builder.RegisterType<Worker>().As<IWorker>();
            builder.RegisterType<Reader>().As<IReader>();
            builder.RegisterType<Compressor>().As<ICompressor>();
            builder.RegisterType<Decompressor>().As<IDecompressor>();
            builder.RegisterType<Writer>().As<IWriter>();
            builder.RegisterType<LoggerFactory>().As<ILoggerFactory>().SingleInstance();

            builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();

            _serviceContainer = builder.Build();
        }

        static void Dispose()
        {
            if (!_isDisposed)
            {
                _tokenSource?.Cancel();

                if (_app is IDisposable)
                    ((IDisposable)_app)?.Dispose();

                _serviceContainer?.Dispose();
                _tokenSource.Dispose();
                LogManager.Shutdown();

                _isDisposed = true;
            }
        }

        static int Cancel()
        {
            Console.WriteLine("Start operation canceling..");
            Dispose();
            Console.WriteLine("Operation was canceled successfully..");

            return 1;
        }
    }
}