using System;
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

        public ICollection<FunctionDeclStatement> Functions
        {
            get
            {
                return _functions.Values;
            }
        }

        public ICollection<VariableDeclStatement?> Variables
        {
            get
            {
                return _variables.Values;
            }
        }

        private readonly Dictionary<string, FunctionDeclStatement> _functions =
            new Dictionary<string, FunctionDeclStatement>();
        private readonly Dictionary<string, VariableDeclStatement?> _variables =
            new Dictionary<string, VariableDeclStatement?>();

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

        /// <summary>
        /// Get a function in the current scope.
        /// </summary>
        /// <param name="identifier">The name of the function.</param>
        /// <returns>Null if none was found.</returns>
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

        /// <summary>
        /// Get a variable in the current scope.
        /// </summary>
        /// <param name="identifier">The name of the variable.</param>
        /// <returns>Null if none was found.</returns>
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