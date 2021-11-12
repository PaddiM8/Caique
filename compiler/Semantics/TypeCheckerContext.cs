using System;
using System.Collections.Generic;
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

        public CheckedClassDeclStatement? CurrentCheckedClass { get; set; }

        public ClassDeclStatement? CurrentClassDecl { get; set; }

        public FunctionDeclStatement? CurrentFunctionDecl { get; set; }

        public Expression? Expression { get; init; }

        public Statement? Statement { get; init; }

        public CheckedExpression? CheckedExpression { get; set; }

        public List<IDataType>? TypeArgumentsForClass { get; set; }

        public TypeCheckerContext CreateChild()
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentCheckedClass = CurrentCheckedClass,
                CurrentClassDecl = CurrentClassDecl,
                CurrentFunctionDecl = CurrentFunctionDecl,
                TypeArgumentsForClass = TypeArgumentsForClass,
            };
        }

        public TypeCheckerContext CreateChild(Expression expression)
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentCheckedClass = CurrentCheckedClass,
                CurrentClassDecl = CurrentClassDecl,
                CurrentFunctionDecl = CurrentFunctionDecl,
                Expression = expression,
                TypeArgumentsForClass = TypeArgumentsForClass,
            };
        }

        public TypeCheckerContext CreateChild(Statement statement)
        {
            return new TypeCheckerContext
            {
                Parent = this,
                CurrentFunctionType = CurrentFunctionType,
                CurrentExtendedType = CurrentExtendedType,
                CurrentCheckedClass = CurrentCheckedClass,
                CurrentClassDecl = CurrentClassDecl,
                CurrentFunctionDecl = CurrentFunctionDecl,
                Statement = statement,
                TypeArgumentsForClass = TypeArgumentsForClass,
            };
        }
    }
}