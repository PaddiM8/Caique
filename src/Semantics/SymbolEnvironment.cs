﻿using System;
using System.Collections.Generic;
using System.Linq;
using Caique.AST;

namespace Caique.Semantics
{
    public class SymbolEnvironment
    {
        public SymbolEnvironment? Parent { get; }

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

        public void Print(int layer = 0)
        {
            string padding = string.Join("", Enumerable.Repeat("┃  ", layer)) + "┣━ ";
            Console.ForegroundColor = ConsoleColor.DarkCyan;

            foreach (var (_, function) in _functions)
            {
                Console.WriteLine(padding + function.Identifier.Value + "()");
            }

            foreach (var (_, variable) in _variables)
            {
                Console.WriteLine(padding + variable!.Identifier.Value);
            }

            Console.ResetColor();
        }
    }
}