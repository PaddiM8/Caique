using System;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace Caique
{
    class Program
    {
        public static CommandLineOptions? Options { get; private set; }
        static void Main(string[] args)
        {
            // Create the cli parser with HelpWriter disabled,
            // since a custom help text will be used
            var cliParser = new CommandLine.Parser(with => with.HelpWriter = null);
            var cliParserResult = cliParser.ParseArguments<CommandLineOptions>(args);

            // Use the command line options
            cliParserResult.WithParsed(options =>
            {
                Options = options;

                // Do something with the options...
                new Compilation(File.ReadAllText(options.InputFile));
            });

            // Show the help text if there was an error
            cliParserResult.WithNotParsed(errors => PrintHelp(cliParserResult));
        }

        private static void PrintHelp(ParserResult<CommandLineOptions> result)
        {
            var helpText = HelpText.AutoBuild(result, help =>
            {
                help.AdditionalNewLineAfterOption = false;
                help.Copyright = "";

                return help;
            }, e => e);

            Console.WriteLine(helpText);
        }
    }
}
