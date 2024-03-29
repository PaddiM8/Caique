﻿using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class VariableDeclStatement : Statement
    {
        public Token Identifier { get; }

        public Expression? Value { get; }

        public TypeExpression? SpecifiedType { get; }

        public VariableType VariableType { get; }

        public int IndexInObject { get; set; }

        public VariableDeclStatement(Token identifier,
                                     TextSpan span,
                                     Expression? value,
                                     VariableType variableType,
                                     TypeExpression? type = null,
                                     int indexInObject = 0)
                                     : base(span)
        {
            Identifier = identifier;
            Value = value;
            SpecifiedType = type;
            VariableType = variableType;
            IndexInObject = indexInObject;
        }
    }
}