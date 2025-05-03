using System.CommandLine;
using Caique;
using Caique.Cli;
using Caique.Cli.Handlers;

class Program
{
    public static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("Caique");

        rootCommand.AddCommand(BuildBuild());
        await rootCommand.InvokeAsync(args);
    }

    private static Command BuildBuild()
    {
        var buildCommand = new Command("build");
        var projectArgument = new Argument<string>("project", "Path to the project folder");
        buildCommand.AddArgument(projectArgument);

        var irOption = new Option<bool>("--dump-ir", "Output LLVM IR.");
        buildCommand.AddOption(irOption);

        buildCommand.SetHandler(BuildHandler.Handle, projectArgument, irOption);

        return buildCommand;
    }
}
