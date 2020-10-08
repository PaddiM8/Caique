using System;
using CommandLine;

namespace Caique.CLI.Options
{
    [Verb("new", HelpText = "Create a new Caique project.")]
    public class NewOptions
    {
        [Value(0, MetaName = "project name", HelpText = "Name of the project.")]
        public string? ProjectName { get; set; }
    }
}