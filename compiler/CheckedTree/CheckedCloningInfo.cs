using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedCloningInfo
    {
        public List<Token>? TypeParameters { get; init; }

        public List<IDataType>? TypeArguments { get; init; }
    }
}