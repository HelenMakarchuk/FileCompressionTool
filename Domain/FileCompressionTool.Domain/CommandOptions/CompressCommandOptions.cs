using CommandLine;

namespace FileCompressionTool.Domain.CommandOptions
{
    [Verb("compress", HelpText = "Compress file\n\ncompress [source file name] [target file name]")]
    public class CompressCommandOptions : ICommandOptions
    {
        [Value(0, MetaName = "Source file path", HelpText = "Source file path", Required = true)]
        public string SourcePath { get; private set; }

        [Value(1, MetaName = "Target file path", HelpText = "Target file path", Required = true)]
        public string TargetPath { get; private set; }
    }
}
