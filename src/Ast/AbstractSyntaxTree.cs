using System;
using System.Collections.Generic;
using Caique.Semantics;

namespace Caique.Ast
{
    public class AbstractSyntaxTree
    {
        public List<Statement> Statements { get; set; }

        public ModuleEnvironment ModuleEnvironment { get; set; }

        public AbstractSyntaxTree(List<Statement> statements, ModuleEnvironment environment)
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