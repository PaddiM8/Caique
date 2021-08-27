using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedUseStatement : CheckedStatement
    {
        public ModuleEnvironment ModuleEnvironment { get; }

        public CheckedUseStatement(ModuleEnvironment moduleEnvironment)
        {
            ModuleEnvironment = moduleEnvironment;
        }
    }
}