using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class VariableDeclStatement : Statement
    {
        public Token Identifier { get; }

        public Expression? Value { get; }

        public TypeExpression? SpecifiedType { get; }

        public DataType? DataType { get; set; }

        public VariableDeclStatement(Token identifier, TextSpan span,
                                     Expression? value, TypeExpression? type = null)
                                     : base(span)
        {
            Identifier = identifier;
            Value = value;
            SpecifiedType = type;
        }
    }
}