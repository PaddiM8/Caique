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

        [Value(0, MetaName = "input file", HelpText = "Path to the Caique input file")]
        public string? InputFile { get; set; }
    }
}