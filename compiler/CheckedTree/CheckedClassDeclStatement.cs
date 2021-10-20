using System;
using System.Collections.Generic;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedClassDeclStatement : CheckedStatement
    {
        public Token Identifier { get; }

        public string FullName => Identifier.Value;

        public CheckedBlockExpression? Body { get; set; }

        public SymbolEnvironment Environment { get; }

        public CheckedFunctionDeclStatement? InitFunction { get; set; }

        public CheckedClassDeclStatement? Inherited => ((StructType?)InheritedType)?.StructDecl;

        public ModuleEnvironment Module { get; }

        public IDataType? InheritedType { get; }

        public IDataType DataType { get; }

        public CheckedClassDeclStatement(Token identifier,
                                         SymbolEnvironment environment,
                                         ModuleEnvironment module,
                                         IDataType? ancestor = null)
        {
            Identifier = identifier;
            Environment = environment;
            InheritedType = ancestor;
            Module = module;
            DataType = new StructType(TypeKeyword.Identifier, this);
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
        public FunctionSymbol? GetFunction(string identifier)
        {
            // Attempt to get the function from the current class,
            // but if it is not found there, try call this method
            // from the ancestor instead (if there is one),
            // in order to try to find it there.
            var function = Environment.GetFunction(identifier, false);
            if (function != null) return function;
            else return Inherited?.GetFunction(identifier);
        }

        public bool HasAncestor(string identifier)
        {
            // The ancestor was found
            if (Inherited?.Identifier.Value == identifier)
                return true;

            return Inherited?.HasAncestor(identifier)
                ?? false; // Return false if "Inherited" is null (there are no more ancestors to compare)
        }
    }
}