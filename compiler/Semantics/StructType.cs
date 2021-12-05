using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique struct-based type (eg. class).
    /// </summary>
    public class StructType : IDataType
    {
        public TypeKeyword Type { get; }

        public List<IDataType>? TypeArguments { get; }

        public CheckedClassDeclStatement StructDecl { get; }

        public bool IsExplicitPointer { get; set; }

        public bool IsAllocated => true;

        public bool IsNumber => false;

        public bool IsString => Type == TypeKeyword.i8 && IsExplicitPointer;

        public bool IsFloat => false;

        public bool IsInt => false;

        public StructType(TypeKeyword type,
                          List<IDataType>? typeArguments,
                          CheckedClassDeclStatement structDecl,
                          bool isExplicitPointer = false)
        {
            Type = type;
            TypeArguments = typeArguments;
            StructDecl = structDecl;
            IsExplicitPointer = isExplicitPointer;
        }

        /// <summary>
        /// Checks if two types are compatible with each other.
        /// </summary>
        public bool IsCompatible(IDataType expected)
        {
            if (expected is StructType expectedStruct)
            {
                // String
                if (Type == TypeKeyword.StringConstant && expected.IsString ||
                    expected.Type == TypeKeyword.StringConstant && IsString)
                    return true;


                // Objects
                if (StructDecl != null && expectedStruct.StructDecl != null)
                {
                    var expectedIdentifier = expectedStruct.StructDecl.Identifier;
                    if (StructDecl.HasAncestor(expectedIdentifier!.Value))
                        return true;

                    return StructDecl?.Identifier!.Value == expectedIdentifier!.Value;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return StructDecl.Identifier.Value
                + SemanticUtils.GetTypeArgumentString(TypeArguments)
                + (IsExplicitPointer ? "*" : "");
        }

        public IDataType Clone(CheckedCloningInfo cloningInfo)
        {
            var newTypeArguments = new List<IDataType>(TypeArguments?.Count ?? 0);
            var newStructDecl = StructDecl;
            if (TypeArguments != null)
            {
                foreach (var typeArgument in TypeArguments)
                {
                    newTypeArguments.Add(typeArgument.Clone(cloningInfo));
                }

                var symbol = StructDecl.Module.GetClass(StructDecl.Identifier.Value, false);
                newStructDecl = cloningInfo.TypeChecker.NextClassDecl(
                    symbol!.Syntax,
                    newTypeArguments.Any() ? newTypeArguments : null
                );
            }

            return new StructType(
                Type,
                newTypeArguments,
                newStructDecl,
                IsExplicitPointer
            );
        }
    }
}