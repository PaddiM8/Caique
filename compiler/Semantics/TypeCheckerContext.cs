using System;
using Caique.Ast;

namespace Caique.Semantics
{
    public class TypeCheckerContext
    {
        public TypeCheckerContext? Parent { get; init; }

        public DataType? DataType { get; set; }

        public DataType? CurrentFunctionType { get; set; }

        public ClassDeclStatement? CurrentObject { get; set; }

        public Expression? Expression { get; set; }

        public TypeCheckerContext CreateChild()
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentObject = CurrentObject,
            };
        }

        public TypeCheckerContext CreateChild(Expression expression)
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentObject = CurrentObject,
                Expression = expression,
            };
        }
    }
}