using System;
using System.Linq;

namespace FileCompressionTool.Domain.Exceptions
{
    public class CommandRunException : Exception
    {
        const string _title = "An error occurred during command execution";

        public CommandRunException()
          : base(_title)
        {
        }

        public CommandRunException(string message)
            : base($"{_title}: \n{message}")
        {
        }

        public CommandRunException(string message, Exception innerEx)
            : base($"{_title}: \n{message}", innerEx)
        {
        }

        public CommandRunException(AggregateException aggregateEx)
            : base($"{_title}: \n{ String.Join('\n', aggregateEx.InnerExceptions.Select(ex => ex.Message)) }", aggregateEx)
        {
        }
    }
}