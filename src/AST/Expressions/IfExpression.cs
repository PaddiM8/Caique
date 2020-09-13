﻿using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class IfExpression : IExpression
    {
        public IExpression Condition { get; }

        public IStatement Branch { get; }

        public IStatement? ElseBranch { get; }

        public IfExpression(IExpression condition, IStatement branch, IStatement? elseBranch)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}