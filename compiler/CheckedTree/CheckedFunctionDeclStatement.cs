using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedFunctionDeclStatement : CheckedStatement
    {
        public Token Identifier { get; }

        public string FullName => FullNameWithoutTypeArguments
            + SemanticUtils.GetTypeArgumentString(TypeArguments);

        public string FullNameWithoutTypeArguments
        {
            get
            {
                if (IsExtensionFunction)
                {
                    return ExtensionOf!.ToString() + "." + Identifier.Value;
                }
                else if (IsMethod)
                {
                    return ParentObject!.FullName + "." + Identifier.Value;
                }
                else
                {
                    return Identifier.Value;
                }
            }
        }

        public List<IDataType>? TypeArguments { get; }

        public List<CheckedVariableDeclStatement> Parameters { get; }

        public CheckedBlockExpression? Body { get; set; }

        public IDataType ReturnType { get; }

        public CheckedClassDeclStatement? ParentObject { get; }

        public ModuleEnvironment? Module { get; }

        public bool IsInitFunction { get; }

        public bool IsVirtual { get; }

        public bool IsOverride { get; }

        public bool IsMethod { get => ParentObject != null; }

        public IDataType? ExtensionOf { get; }

        public bool IsExtensionFunction => ExtensionOf != null;

        public int? IndexInVirtualMethodTable { get; set; }

        public Dictionary<int, CheckedFunctionDeclStatement>? Overrides { get; }

        public CheckedFunctionDeclStatement(Token identifier,
                                            List<IDataType>? typeArguments,
                                            List<CheckedVariableDeclStatement> parameters,
                                            CheckedBlockExpression? body,
                                            IDataType returnType,
                                            bool isInitFunction,
                                            bool isVirtual,
                                            bool isOverride,
                                            CheckedClassDeclStatement? parentObject,
                                            ModuleEnvironment? module,
                                            IDataType? extensionOf = null)
        {
            Identifier = identifier;
            TypeArguments = typeArguments;
            Parameters = parameters;
            Body = body;
            IsInitFunction = isInitFunction;
            IsVirtual = isVirtual;
            IsOverride = isOverride;
            ReturnType = returnType;
            ParentObject = parentObject;
            Module = module;
            ExtensionOf = extensionOf;

            if (isVirtual)
            {
                Overrides = new();
                Overrides.Add(parentObject!.Id, this);
            }
        }

        public void RegisterOverride(CheckedFunctionDeclStatement checkedFunction)
        {
            if (!IsVirtual) throw new InvalidOperationException("Can't add an override function to a non-virtual function.");
            Overrides!.Add(checkedFunction.ParentObject!.Id, checkedFunction);
        }
    }
}