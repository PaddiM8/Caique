
using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedKeywordValueExpression : CheckedExpression
    {
        public TokenKind TokenKind { get; }

        public CheckedKeywordValueExpression(TokenKind tokenKind, IDataType dataType) : base(dataType)
        {
            TokenKind = tokenKind;
        }
    }
}