using System;
using System.Collections.Generic;
using Caique.Semantics;

namespace Caique.Ast
{
    public class AbstractSyntaxTree
    {
        public List<IStatement> Statements { get; set; }

        public ModuleEnvironment ModuleEnvironment { get; set; }

        public AbstractSyntaxTree(List<IStatement> statements, ModuleEnvironment environment)
        {
            Statements = statements;
            ModuleEnvironment = environment;
        }

        public void Print()
        {
            new AstPrinter(this).Print();
        }
    }
}