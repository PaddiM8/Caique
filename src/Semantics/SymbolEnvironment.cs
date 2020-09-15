using System;
using System.Collections.Generic;
using Caique.AST;

namespace Caique.Semantics
{
    public class SymbolEnvironment
    {
        public SymbolEnvironment? Parent { get; }

        private readonly Dictionary<string, ClassDeclStatement> _classes =
            new Dictionary<string, ClassDeclStatement>();
        private readonly Dictionary<string, FunctionDeclStatement> _functions =
            new Dictionary<string, FunctionDeclStatement>();
        private readonly Dictionary<string, VariableDeclStatement?> _variables =
            new Dictionary<string, VariableDeclStatement?>();

        public SymbolEnvironment(SymbolEnvironment? parent = null)
        {
            Parent = parent;
        }

        public SymbolEnvironment CreateChildEnvironment()
        {
            return new SymbolEnvironment(this);
        }

        public void Add(ClassDeclStatement classDecl)
        {
            _classes.Add(classDecl.Identifier.Value, classDecl);
        }

        public void Add(FunctionDeclStatement function)
        {
            _functions.Add(function.Identifier.Value, function);
        }

        public void Add(VariableDeclStatement variable)
        {
            if (ContainsVariable(variable.Identifier.Value))
            {
                throw new ArgumentException("Symbol already exists.");
            }

            _variables.Add(variable.Identifier.Value, variable);
        }

        public ClassDeclStatement? GetClass(string identifier)
        {
            _classes.TryGetValue(identifier, out ClassDeclStatement? classDecl);

            if (classDecl != null)
            {
                return classDecl;
            }
            else if (Parent != null)
            {
                return Parent.GetClass(identifier);
            }
            else
            {
                return null;
            }
        }

        public FunctionDeclStatement? GetFunction(string identifier)
        {
            _functions.TryGetValue(identifier, out FunctionDeclStatement? function);

            if (function != null)
            {
                return function;
            }
            else if (Parent != null)
            {
                return Parent.GetFunction(identifier);
            }
            else
            {
                return null;
            }
        }

        public VariableDeclStatement? GetVariable(string identifier)
        {
            _variables.TryGetValue(identifier, out VariableDeclStatement? variable);

            if (variable != null)
            {
                return variable;
            }
            else if (Parent != null)
            {
                return Parent.GetVariable(identifier);
            }
            else
            {
                return null;
            }
        }

        public bool ContainsVariable(string identifier)
        {
            if (_variables.ContainsKey(identifier))
                return true;

            // If the parent or its ancestors contain the variable
            return Parent != null && Parent.ContainsVariable(identifier);
        }
    }
}