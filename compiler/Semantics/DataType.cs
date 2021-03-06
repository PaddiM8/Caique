﻿using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public class DataType
    {
        public TypeKeyword Type { get; }

        public ClassDeclStatement? ObjectDecl { get; }

        public bool IsExplicitPointer { get; set; }

        public bool Allocated { get; private set; }

        public bool IsNumber { get => IsInt || IsFloat; }

        public bool IsInt =>
            Type switch
            {
                TypeKeyword.i8 or
                TypeKeyword.i32 or
                TypeKeyword.i64 => true,
                _ => false,
            };

        public bool IsFloat =>
            Type switch
            {
                TypeKeyword.f8 or
                TypeKeyword.f32 or
                TypeKeyword.f64 => true,
                _ => false,
            };

        public bool IsString =>
            Type == TypeKeyword.i8 && IsExplicitPointer;

        public bool IsObject =>
            ObjectDecl != null;

        public DataType(TypeKeyword type, ClassDeclStatement? objectDecl = null, bool isExplicitPointer = false)
        {
            Type = type;
            ObjectDecl = objectDecl;
            IsExplicitPointer = isExplicitPointer;
        }

        /// <summary>
        /// Checks if two types are compatible with each other.
        /// </summary>
        public bool IsCompatible(DataType expected)
        {
            // String
            if (Type == TypeKeyword.StringConstant && expected.IsString ||
                expected.Type == TypeKeyword.StringConstant && IsString)
                return true;


            // Objects
            if (ObjectDecl != null && expected.ObjectDecl != null)
            {
                var expectedIdentifier = expected.ObjectDecl.Identifier;
                if (ObjectDecl.HasAncestor(expectedIdentifier!.Value))
                    return true;

                return ObjectDecl?.Identifier!.Value == expectedIdentifier!.Value;
            }

            return Type == expected.Type;
        }

        public override string ToString()
        {
            string baseType = Type == TypeKeyword.Identifier
                ? ObjectDecl!.Identifier.Value
                : Type.ToString().ToLower();

            return baseType + (IsExplicitPointer ? "*" : "");
        }
    }
}