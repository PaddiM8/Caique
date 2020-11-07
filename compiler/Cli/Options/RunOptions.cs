using System;
using CommandLine;

namespace Caique.Cli.Options
{
    [Verb("run", HelpText = "Run the project.")]
    public class RunOptions
    {
        [Value("std-path", HelpText = "Path to the standard library object files.")]
        public string? StdPath { get; set; }

        [Value(0, MetaName = "project file path", HelpText = "Path to the project.toml file")]
        public string? ProjectFilePath { get; set; }
    }
}