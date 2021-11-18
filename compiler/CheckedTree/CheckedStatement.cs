using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedStatement
    {
        public virtual CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            throw new NotImplementedException();
        }
    }
}