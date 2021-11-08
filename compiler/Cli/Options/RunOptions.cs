using System;
using CommandLine;

namespace Caique.Cli.Options
{
    [Verb("run", HelpText = "Run the project.")]
    public class RunOptions
    {
        [Option("ast", Required = false, HelpText = "Print the Abstract Syntax Tree")]
        public bool PrintAst { get; set; }

        [Option("tokens", Required = false, HelpText = "Print the token list made by the lexer")]
        public bool PrintTokens { get; set; }

        [Option("environment", Required = false, HelpText = "Print the module/symbol environment")]
        public bool PrintEnvironment { get; set; }

        [Option("llvm", Required = false, HelpText = "Print LLVM IR")]
        public bool PrintLlvm { get; set; }

        [Option("show-linker-output", Required = false, HelpText = "Show the linker output")]
        public bool ShowLinkerOutput { get; set; }

        [Option("std-path", Required = false, HelpText = "Path to the standard library object files.", Default = "std/")]
        public string StdPath { get; set; } = "";

        [Value(0, MetaName = "project file path", HelpText = "Path to the project.toml file")]
        public string? ProjectFilePath { get; set; } = "project.toml";
    }
}