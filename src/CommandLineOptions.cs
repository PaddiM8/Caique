using System;
using CommandLine;

namespace Caique
{
    class CommandLineOptions
    {
        [Option("ast", Required = false, HelpText = "Print the Abstract Syntax Tree")]
        public bool PrintAst { get; set; }

        [Option("tokens", Required = false, HelpText = "Print the token list made by the lexer")]
        public bool PrintTokens { get; set; }

        [Option("environment", Required = false, HelpText = "Print the module/symbol environment")]
        public bool PrintEnvironment { get; set; }

        [Value(0, MetaName = "project file path", HelpText = "Path to the project.toml file")]
        public string? ProjectFilePath { get; set; }
    }
}