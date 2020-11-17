using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class ClassDeclStatement : Statement
    {
        public Token Identifier { get; }

        public BlockExpression Body { get; }

        public FunctionDeclStatement? InitFunction { get; set; }

        public ClassDeclStatement? Inherited
        {
            get
            {
                if (_ancestorType == null) return null;

                return Module.GetClass(_ancestorType.ModulePath[^1].Value);
            }
        }

        public ModuleEnvironment Module { get; }

        private readonly TypeExpression? _ancestorType;

        public ClassDeclStatement(Token identifier,
                                  BlockExpression body,
                                  TextSpan span,
                                  ModuleEnvironment module,
                                  TypeExpression? ancestor = null,
                                  FunctionDeclStatement? initFunction = null) : base(span)
        {
            Identifier = identifier;
            Body = body;
            _ancestorType = ancestor;
            Module = module;
            InitFunction = initFunction;
        }

        /// <summary>
        /// Gets an object variable, and also looks inside ancestor objects.
        /// </summary>
        /// <param name="identifier">Name of the variable to find.</param>
        public VariableDeclStatement? GetVariable(string identifier)
        {
            // Attempt to get the variable from the current class,
            // but if it is not found there, try call this method
            // from the ancestor instead (if there is one),
            // in order to try to find it there.
            return Body.Environment.GetVariable(identifier)
                ?? Inherited?.GetVariable(identifier);
        }

        /// <summary>
        /// Gets an object function, and also looks inside ancestor objects.
        /// </summary>
        /// <param name="identifier">Name of the function to find.</param>
        public FunctionDeclStatement? GetFunction(string identifier)
        {
            // Attempt to get the function from the current class,
            // but if it is not found there, try call this method
            // from the ancestor instead (if there is one),
            // in order to try to find it there.
            return Body.Environment.GetFunction(identifier)
                ?? Inherited?.GetFunction(identifier);
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