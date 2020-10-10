using System;
using Caique.Ast;

namespace Caique.Semantics
{
    public class TypeCheckerContext
    {
        public TypeCheckerContext? Parent { get; init; }

        public DataType? DataType { get; set; }

        public DataType? FunctionType { get; set; }

        public TypeCheckerContext CreateChild()
        {
            return new TypeCheckerContext
            {
                Parent = this,
                FunctionType = FunctionType
            };
        }
    }
}