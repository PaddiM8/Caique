using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    class CheckedUnknownStatement : CheckedStatement
    {
        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedUnknownStatement();
        }
    }
}