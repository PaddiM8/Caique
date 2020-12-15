using System;
using Caique.Ast;

namespace Caique.Semantics
{
    public class TypeCheckerContext
    {
        public TypeCheckerContext? Parent { get; init; }

        public DataType? DataType { get; set; }

        public DataType? CurrentFunctionType { get; set; }

        public DataType? CurrentExtendedType { get; set; }

        public ClassDeclStatement? CurrentObject { get; set; }

        public Expression? Expression { get; set; }

        public TypeCheckerContext CreateChild()
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentObject = CurrentObject,
            };
        }

        public TypeCheckerContext CreateChild(Expression expression)
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentObject = CurrentObject,
                Expression = expression,
            };
        }
    }
}