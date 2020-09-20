using System;
using System.Collections.Generic;
using Caique.Semantics;

namespace Caique.AST
{
    public class Ast
    {
        public List<IStatement> Statements { get; set; }

        public ModuleEnvironment ModuleEnvironment { get; set; }

        public Ast(List<IStatement> statements, ModuleEnvironment environment)
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