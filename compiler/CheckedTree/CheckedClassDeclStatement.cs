using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedClassDeclStatement : CheckedStatement
    {
        public int Id { get; }

        public Token Identifier { get; }

        public string FullName => DataType.ToString();

        public List<IDataType>? TypeArguments { get; }

        public CheckedBlockExpression? Body { get; set; }

        public SymbolEnvironment Environment { get; }

        public CheckedFunctionDeclStatement? InitFunction { get; set; }

        public CheckedClassDeclStatement? Inherited => InheritedType?.StructDecl;

        public ModuleEnvironment Module { get; }

        public bool ShouldBeEmitted { get; }

        public List<CheckedFunctionDeclStatement>? VirtualMethods { get; private set; }

        public StructType? InheritedType { get; }

        public IDataType DataType { get; }

        private int _highestClassId;

        public CheckedClassDeclStatement(Token identifier,
                                         List<IDataType>? typeArguments,
                                         SymbolEnvironment environment,
                                         ModuleEnvironment module,
                                         StructType? ancestor = null,
                                         bool shouldBeEmitted = true)
        {
            Identifier = identifier;
            TypeArguments = typeArguments;
            Environment = environment;
            InheritedType = ancestor;
            Module = module;
            DataType = new StructType(TypeKeyword.Identifier, typeArguments, this);
            ShouldBeEmitted = shouldBeEmitted;

            CheckedClassDeclStatement? inheritedClass = this;
            while (inheritedClass.Inherited != null)
            {
                inheritedClass = inheritedClass.Inherited;
            }

            Id = inheritedClass == this
                ? 0
                : ++inheritedClass._highestClassId;
        }

        /// <summary>
        /// Gets an object variable, and also looks inside ancestor objects.
        /// </summary>
        /// <param name="identifier">Name of the variable to find.</param>
        public VariableSymbol? GetVariable(string identifier)
        {
            // Attempt to get the variable from the current class,
            // but if it is not found there, try call this method
            // from the ancestor instead (if there is one),
            // in order to try to find it there.
            return Environment.GetVariable(identifier, false)
                ?? Inherited?.GetVariable(identifier);
        }

        /// <summary>
        /// Gets an object function, and also looks inside ancestor objects.
        /// </summary>
        /// <param name="identifier">Name of the function to find.</param>
        public CheckedFunctionDeclStatement? GetFunction(string identifier,
                                                         List<IDataType>? typeArguments = null,
                                                         bool lookInInherited = true)
        {
            // Attempt to get the function from the current class,
            // but if it is not found there, try call this method
            // from the ancestor instead (if there is one),
            // in order to try to find it there.
            var function = Environment.GetFunction(identifier, false);
            if (function != null) return function.GetCheckedFromClass(this, typeArguments);
            else return lookInInherited ? Inherited?.GetFunction(identifier, typeArguments) : null;
        }

        public CheckedClassDeclStatement? GetParentClassForFunction(string identifier)
        {
            if (Environment.GetFunction(identifier, false) != null)
                return this;

            return Inherited?.GetParentClassForFunction(identifier);
        }

        public bool HasAncestor(string identifier)
        {
            // The ancestor was found
            if (Inherited?.Identifier.Value == identifier)
                return true;

            return Inherited?.HasAncestor(identifier)
                ?? false; // Return false if "Inherited" is null (there are no more ancestors to compare)
        }

        public void RegisterVirtualMethod(CheckedFunctionDeclStatement method)
        {
            if (VirtualMethods == null) VirtualMethods = new();
            method.IndexInVirtualMethodTable = VirtualMethods.Count;
            VirtualMethods.Add(method);
        }
    }
}