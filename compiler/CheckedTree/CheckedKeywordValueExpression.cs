
using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedKeywordValueExpression : CheckedExpression
    {
        public TokenKind TokenKind { get; }

        public List<CheckedExpression>? Arguments { get; }

        public CheckedKeywordValueExpression(TokenKind tokenKind,
                                             List<CheckedExpression>? arguments,
                                             IDataType dataType) : base(dataType)
        {
            TokenKind = tokenKind;
            Arguments = arguments;
        }
    }
}