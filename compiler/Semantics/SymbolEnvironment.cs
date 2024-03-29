﻿using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;

namespace Caique.Semantics
{
    /// <summary>
    /// Symbol table of functions and variables within classes.
    /// </summary>
    public class SymbolEnvironment
    {
        public SymbolEnvironment? Parent { get; }

        public StructSymbol? ParentObject =>
            _parentObject ?? Parent?.ParentObject;

        public ICollection<StructSymbol> Classes =>
            _classes.Values;

        public ICollection<FunctionSymbol> Functions =>
            _functions.Values;

        public ICollection<VariableSymbol?> Variables =>
            _variables.Values;

        private StructSymbol? _parentObject;

        private readonly Dictionary<string, StructSymbol> _classes = new();
        private readonly Dictionary<string, FunctionSymbol> _functions = new();
        private readonly Dictionary<string, VariableSymbol?> _variables = new();

        public SymbolEnvironment(SymbolEnvironment? parent = null)
        {
            Parent = parent;
        }

        /// <summary>
        /// Create a new symbol environment inside this one.
        /// </summary>
        /// <returns>The new symbol environment.</returns>
        public SymbolEnvironment CreateChildEnvironment()
        {
            return new SymbolEnvironment(this);
        }

        public void Add(ClassDeclStatement classDecl)
        {
            var symbol = new StructSymbol(classDecl);
            _classes.Add(classDecl.Identifier.Value, symbol);
            classDecl.Body.Environment._parentObject = symbol;
        }

        public void Add(FunctionDeclStatement function)
        {
            _functions.Add(function.FullName, new FunctionSymbol(function));
        }

        public bool TryAdd(VariableDeclStatement variable)
        {
            return TryAdd(variable, out VariableSymbol? _);
        }

        public bool TryAdd(VariableDeclStatement variable, out VariableSymbol? symbol)
        {
            var tryGetVariable = GetVariable(variable.Identifier.Value, false);
            if (tryGetVariable != null)
            {
                symbol = tryGetVariable;

                return false;
            }

            symbol = new VariableSymbol(variable, this);
            _variables.Add(variable.Identifier.Value, symbol);

            return true;
        }

        /// <summary>
        /// Get a class
        /// </summary>
        /// <param name="identifier">The name of the class.</param>
        /// <returns>Null if none was found.</returns>
        public StructSymbol? GetClass(string identifier)
        {
            _classes.TryGetValue(identifier, out StructSymbol? classDecl);
            if (classDecl != null) return classDecl;

            SymbolEnvironment? parent = Parent;
            while (parent != null)
            {
                if (Parent!._classes.TryGetValue(identifier, out StructSymbol? parentClassDecl))
                    return parentClassDecl;
            }

            return null;
        }

        /// <summary>
        /// Get a function in the current scope.
        /// </summary>
        /// <param name="identifier">The name of the function.</param>
        /// <param name="lookInParentScopes">If this is false, it will only look in the current SymbolEnvironment.</param>
        /// <returns>Null if none was found.</returns>
        public FunctionSymbol? GetFunction(string identifier, bool lookInParentScopes = true)
        {
            _functions.TryGetValue(identifier, out FunctionSymbol? function);
            if (function != null) return function;
            if (!lookInParentScopes) return null;

            // Shouldn't matter which variation since they all have the same parent
            var checkedParent = ParentObject?.AllChecked.First();
            var fromAncestor = checkedParent?.Inherited?.Environment.GetFunction(identifier);
            if (fromAncestor != null) return fromAncestor;

            var parentFunction = Parent?.GetFunction(identifier);
            if (parentFunction != null) return parentFunction;

            return null;
        }

        /// <summary>
        /// Get a variable in the current scope.
        /// </summary>
        /// <param name="identifier">The name of the variable.</param>
        /// <param name="lookInParentScopes">If this is false, it will only look in the current SymbolEnvironment.</param>
        /// <returns>Null if none was found.</returns>
        public VariableSymbol? GetVariable(string identifier, bool lookInParentScopes = true)
        {
            _variables.TryGetValue(identifier, out VariableSymbol? variable);
            if (variable != null) return variable;
            if (!lookInParentScopes) return null;

            // Shouldn't matter which variation since they all have the same parent
            var checkedParent = ParentObject?.AllChecked.First();
            var fromAncestor = checkedParent?.Inherited?.Environment.GetVariable(identifier);
            if (fromAncestor != null) return fromAncestor;

            SymbolEnvironment? parent = Parent;
            while (parent != null)
            {
                if (parent!._variables.TryGetValue(identifier, out VariableSymbol? parentVariable))
                    return parentVariable;
                
                parent = parent.Parent;
            }

            return null;
        }

        public bool ContainsClass(string identifier)
        {
            return _classes.ContainsKey(identifier);
        }

        public bool ContainsFunction(string identifier)
        {
            return _functions.ContainsKey(identifier);
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