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

        public ClassDeclStatement? ParentObject
        {
            get => _parentObject ?? Parent?.ParentObject;
        }

        public ICollection<ClassDeclStatement> Classes
        {
            get => _classes.Values;
        }

        public ICollection<FunctionDeclStatement> Functions
        {
            get => _functions.Values;
        }

        public ICollection<VariableDeclStatement?> Variables
        {
            get => _variables.Values;
        }

        private ClassDeclStatement? _parentObject;

        private readonly Dictionary<string, ClassDeclStatement> _classes = new();
        private readonly Dictionary<string, FunctionDeclStatement> _functions = new();
        private readonly Dictionary<string, VariableDeclStatement?> _variables = new();

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
            _classes.Add(classDecl.Identifier.Value, classDecl);
            classDecl.Body.Environment._parentObject = classDecl;
        }

        public void Add(FunctionDeclStatement function)
        {
            if (function.IsExtensionFunction)
            {
                string extensionTypeName = function.ExtensionOf!.ModulePath[^1].Value;
                _functions.Add(
                    $"{extensionTypeName}.{function.Identifier.Value}",
                    function
                );

                return;
            }

            _functions.Add(function.Identifier.Value, function);
        }

        public bool TryAdd(VariableDeclStatement variable)
        {
            if (ContainsVariable(variable.Identifier.Value)) return false;

            _variables.Add(variable.Identifier.Value, variable);

            return true;
        }

        /// <summary>
        /// Get a class
        /// </summary>
        /// <param name="identifier">The name of the class.</param>
        /// <returns>Null if none was found.</returns>
        public ClassDeclStatement? GetClass(string identifier)
        {
            _classes.TryGetValue(identifier, out ClassDeclStatement? classDecl);
            if (classDecl != null) return classDecl;

            SymbolEnvironment? parent = Parent;
            while (parent != null)
            {
                if (Parent!._classes.TryGetValue(identifier, out ClassDeclStatement? parentClassDecl))
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
        public FunctionDeclStatement? GetFunction(string identifier, bool lookInParentScopes = true)
        {
            _functions.TryGetValue(identifier, out FunctionDeclStatement? function);
            if (function != null) return function;
            if (!lookInParentScopes) return null;

            var fromAncestor = ParentObject?.Inherited?.GetFunction(identifier);
            if (fromAncestor != null) return fromAncestor;

            SymbolEnvironment? parent = Parent;
            while (parent != null)
            {
                var parentFunction = Parent!.GetFunction(identifier);
                if (parentFunction != null) return parentFunction;
            }

            return null;
        }

        /// <summary>
        /// Get a variable in the current scope.
        /// </summary>
        /// <param name="identifier">The name of the variable.</param>
        /// <param name="lookInParentScopes">If this is false, it will only look in the current SymbolEnvironment.</param>
        /// <returns>Null if none was found.</returns>
        public VariableDeclStatement? GetVariable(string identifier, bool lookInParentScopes = true)
        {
            _variables.TryGetValue(identifier, out VariableDeclStatement? variable);
            if (variable != null) return variable;
            if (!lookInParentScopes) return null;

            var fromAncestor = ParentObject?.Inherited?.GetVariable(identifier);
            if (fromAncestor != null) return fromAncestor;

            SymbolEnvironment? parent = Parent;
            while (parent != null)
            {
                if (Parent!._variables.TryGetValue(identifier, out VariableDeclStatement? parentVariable))
                    return parentVariable;
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