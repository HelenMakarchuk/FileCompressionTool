using Autofac;
using FileCompressionTool.Domain.CommandOptions;
using FileCompressionTool.Domain.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace FileCompressionTool.Application
{
    class App : IApp, IDisposable
    {
        readonly ICommand _command;
        readonly ILogger<DecompressCommand> _logger;
        CancellationTokenSource _tokenSource;
        bool _isDisposed;

        public App(IContainer serviceContainer, ICommandOptions commandOptions)
        {
            _command = serviceContainer.ResolveKeyed<ICommand>(commandOptions.GetType(),
                new TypedParameter(commandOptions.GetType(), commandOptions),
                new NamedParameter("maxWorkersCount", Environment.ProcessorCount));

            _logger = serviceContainer.Resolve<ILogger<DecompressCommand>>();
            _tokenSource = new CancellationTokenSource();
        }

        ~App()
        {
            Dispose(false);
        }

        public void Run(CancellationToken token)
        {
            _logger.LogDebug("Run App");
            _command.Run(token);
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
                _tokenSource.Cancel();

                if (disposeManagedResources)
                {
                    _command.Dispose();
                    _tokenSource.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}