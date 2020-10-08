using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Caique.Cli;
using Caique.Cli.Options;
using CommandLine;
using CommandLine.Text;

namespace Caique
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create the cli parser with HelpWriter disabled,
            // since a custom help text will be used
            var cliParser = new CommandLine.Parser(with => with.HelpWriter = null);
            var cliParserResult = cliParser.ParseArguments(args, LoadVerbs());
            cliParserResult.WithParsed(RunVerb);
            cliParserResult.WithNotParsed(errors => PrintHelp(cliParserResult));
        }

        /// <summary>
        /// Get the objects for the different CLI verbs.
        /// </summary>
        private static Type[] LoadVerbs()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
        }

        private static void RunVerb(object obj)
        {
            switch (obj)
            {
                case BuildOptions options:
                    Project.Load(options.ProjectFilePath!).Build(options);
                    break;
                case NewOptions options:
                    Project.Create(options.ProjectName!);
                    break;
            }
        }

        private static void PrintHelp(ParserResult<object> result)
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
