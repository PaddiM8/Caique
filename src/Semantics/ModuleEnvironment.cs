using System;
using System.Collections.Generic;
using System.Linq;
using Caique.AST;

namespace Caique.Semantics
{
    public class ModuleEnvironment
    {
        public string Identifier { get; }

        public string? FilePath { get; }

        public ModuleEnvironment? Parent;

        public SymbolEnvironment SymbolEnvironment = new SymbolEnvironment();

        public readonly Dictionary<string, ModuleEnvironment> Modules =
            new Dictionary<string, ModuleEnvironment>();

        private readonly Dictionary<string, ClassDeclStatement> _classes =
            new Dictionary<string, ClassDeclStatement>();

        public ModuleEnvironment(string identifier, string? filePath = null, ModuleEnvironment? parent = null)
        {
            Identifier = identifier;
            FilePath = filePath;
            Parent = parent;
        }

        public ModuleEnvironment CreateChildModule(string identifier, string? filePath = null)
        {
            var module = new ModuleEnvironment(identifier, filePath, this);
            Modules[identifier] = module;

            return module;
        }

        public void Add(ClassDeclStatement classDecl)
        {
            _classes.Add(classDecl.Identifier.Value, classDecl);
        }

        public ClassDeclStatement? GetClass(string identifier)
        {
            _classes.TryGetValue(identifier, out ClassDeclStatement? classDecl);

            return classDecl;
        }

        public ModuleEnvironment? FindByPath(IEnumerable<string> identifiers)
        {
            return FindByPath(new Queue<string>(identifiers));
        }

        public ModuleEnvironment? FindByPath(Queue<string> identifiers)
        {
            var identifier = identifiers.Dequeue(); // Start with the first identifier, and make sure the next recursion deals with the next identifier
            if (Modules.TryGetValue(identifier, out ModuleEnvironment? childEnvironment))
            {
                // Proceed to search for the next identifier in the path in the child environment
                return childEnvironment.FindByPath(identifiers);
            }
            else if (_classes.ContainsKey(identifier) ||
                     SymbolEnvironment.ContainsFunction(identifier))
            {
                // If the end-symbol has been reached, stop the recursion and return the module environment.
                return this;
            }
            else // Couldn't be found
            {
                return null;
            }
        }

        public void Print(int layer = 0)
        {
            string padding = string.Join("", Enumerable.Repeat("┃  ", layer)) + "┣━ ";
            Console.WriteLine(padding + Identifier);

            foreach (var (_, child) in Modules)
            {
                child.Print(layer + 1);
            }

            foreach (var (_, classDecl) in _classes)
            {
                Console.Write("┃  " + padding);
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("class: " + classDecl.Identifier.Value);
                Console.ResetColor();
            }

            SymbolEnvironment.Print(layer + 1);
        }
    }
}