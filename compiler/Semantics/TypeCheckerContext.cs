using System;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class TypeCheckerContext
    {
        public TypeCheckerContext? Parent { get; init; }

        public IDataType? DataType { get; set; }

        public IDataType? CurrentFunctionType { get; set; }

        public IDataType? CurrentExtendedType { get; set; }

        public IDataType? ExpectedType { get; set; }

        public CheckedClassDeclStatement? CurrentObject { get; set; }

        public Expression? Expression { get; init; }

        public Statement? Statement { get; init; }

        public CheckedExpression? CheckedExpression { get; set; }

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

        public TypeCheckerContext CreateChild(Statement statement)
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentObject = CurrentObject,
                Statement = statement,
            };
        }
    }
}