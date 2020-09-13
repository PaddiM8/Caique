﻿using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class ExpressionStatement : IStatement
    {
        public IExpression Expression { get; }

        public bool TrailingSemicolon { get; }

        public ExpressionStatement(IExpression expr, bool trailingSemicolon)
        {
            Expression = expr;
            TrailingSemicolon = trailingSemicolon;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}