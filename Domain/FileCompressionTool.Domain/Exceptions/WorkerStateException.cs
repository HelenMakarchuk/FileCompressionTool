using System;

namespace FileCompressionTool.Domain.Exceptions
{
    class WorkerStateException : Exception
    {
        const string _title = "An error occurred during the changing status of the worker";

        public WorkerStateException()
          : base(_title)
        {
        }

        public WorkerStateException(string message)
            : base($"{_title}: {message}")
        {
        }

        public WorkerStateException(string message, Exception innerEx)
            : base($"{_title}: {message}", innerEx)
        {
        }
    }
}