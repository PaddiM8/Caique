﻿using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique struct-based type (eg. class).
    /// </summary>
    public class StructType : IDataType
    {
        public TypeKeyword Type { get; }

        public ClassDeclStatement StructDecl { get; }

        public bool IsExplicitPointer { get; set; }

        public bool Allocated { get; }

        public bool IsNumber => false;

        public bool IsString => Type == TypeKeyword.i8 && IsExplicitPointer;

        public bool IsFloat => false;

        public bool IsInt => false;

        public StructType(TypeKeyword type, ClassDeclStatement structDecl, bool isExplicitPointer = false)
        {
            Type = type;
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

        public override string ToString() =>
            StructDecl.Identifier.Value + (IsExplicitPointer ? "*" : "");
    }
}