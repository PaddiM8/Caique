﻿using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedFunctionDeclStatement : CheckedStatement
    {
        public Token Identifier { get; }

        public List<CheckedVariableDeclStatement> Parameters { get; }

        public CheckedBlockExpression? Body { get; set; }

        public IDataType ReturnType { get; }

        public StructSymbol? ParentObject { get; }

        public ModuleEnvironment? Module { get; }

        public bool IsInitFunction { get; }

        public bool IsMethod { get => ParentObject != null; }

        public IDataType? ExtensionOf { get; }

        public bool IsExtensionFunction => ExtensionOf != null;

        public CheckedFunctionDeclStatement(Token identifier,
                                            List<CheckedVariableDeclStatement> parameters,
                                            CheckedBlockExpression? body,
                                            IDataType returnType,
                                            bool isInitFunction,
                                            StructSymbol? parentObject,
                                            ModuleEnvironment? module,
                                            IDataType? extensionOf = null)
        {
            Identifier = identifier;
            Parameters = parameters;
            Body = body;
            IsInitFunction = isInitFunction;
            ReturnType = returnType;
            ParentObject = parentObject;
            Module = module;
            ExtensionOf = extensionOf;
        }
    }
}