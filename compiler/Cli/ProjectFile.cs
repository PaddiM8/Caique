using System;
using System.Collections.Generic;

namespace Caique.Cli
{
    class ProjectFile
    {
        public string? Name { get; set; }

        public string? Author { get; set; }

        public string? Version { get; set; }

        public Dictionary<string, string>? Dependencies { get; set; }
    }
}