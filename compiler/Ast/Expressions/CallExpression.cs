using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class CallExpression : Expression
    {
        public List<Token> ModulePath { get; }

        public List<Expression> Arguments { get; }

        public FunctionDeclStatement? FunctionDecl { get; set; }

        public CallExpression(List<Token> modulePath,
                              List<Expression> arguments,
                              TextSpan span) : base(span)
        {
            ModulePath = modulePath;
            Arguments = arguments;
        }
    }
}