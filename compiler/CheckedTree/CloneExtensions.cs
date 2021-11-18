using System;
using System.Collections.Generic;
using Caique.CheckedTree;
using Caique.Parsing;
using Caique.Semantics;

static class CloneExtensions
{
    public static List<CheckedExpression> CloneExpressions<T>(this List<T> expressions,
                                                              CheckedCloningInfo cloningInfo) where T : CheckedExpression
    {
        var cloned = new List<CheckedExpression>(expressions.Count);
        foreach (var expression in expressions)
            cloned.Add(expression.Clone(cloningInfo));
        
        return cloned;
    }

    public static List<CheckedStatement> CloneStatements<T>(this List<T> statements,
                                                            CheckedCloningInfo cloningInfo) where T : CheckedStatement
    {
        var cloned = new List<CheckedStatement>(statements.Count);
        foreach (var statement in statements)
            cloned.Add(statement.Clone(cloningInfo));
        
        return cloned;
    }
}