using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedCloningInfo
    {
        public List<Token>? TypeParameters { get; init; }

        public List<IDataType>? TypeArguments { get; init; }

        public CheckedClassDeclStatement? CheckedParentClass { get; set; }

        public TypeChecker TypeChecker { get; }

        public CheckedCloningInfo(TypeChecker typeChecker)
        {
            TypeChecker = typeChecker;
        }
    }
}