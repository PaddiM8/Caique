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

        public ModuleEnvironment Root { get; }

        public readonly SymbolEnvironment SymbolEnvironment
            = new SymbolEnvironment();

        public readonly Dictionary<string, ModuleEnvironment> Modules =
            new Dictionary<string, ModuleEnvironment>();

        private readonly Dictionary<string, ClassDeclStatement> _classes =
            new Dictionary<string, ClassDeclStatement>();
        private readonly List<ModuleEnvironment> _importedModules
            = new List<ModuleEnvironment>();

        public ModuleEnvironment(string identifier, string? filePath = null, ModuleEnvironment? parent = null)
        {
            Identifier = identifier;
            FilePath = filePath;
            Parent = parent;
            Root = Parent == null ? this : Parent.Root;
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

        public void ImportModule(ModuleEnvironment module)
        {
            _importedModules.Add(module);
        }

        public ClassDeclStatement? GetClass(string identifier, bool lookInImports = true)
        {
            if (_classes.TryGetValue(identifier, out ClassDeclStatement? classDecl))
            {
                return classDecl;
            }
            else if (lookInImports)
            {
                // Try to find it in the imports
                foreach (var import in _importedModules)
                {
                    var foundClass = import.GetClass(identifier, false);
                    if (foundClass != null) return foundClass;
                }
            }

            return null;
        }

        public FunctionDeclStatement? GetFunction(string identifier, bool lookInImports = true)
        {
            var functionDecl = SymbolEnvironment.GetFunction(identifier);
            if (functionDecl != null)
            {
                return functionDecl;
            }
            else if (lookInImports)
            {
                // Try to find it in the imports
                foreach (var import in _importedModules)
                {
                    var foundFunction = import.GetFunction(identifier, false);
                    if (foundFunction != null) return foundFunction;
                }
            }

            return null;
        }

        public ModuleEnvironment? FindByPath(IEnumerable<string> identifiers, bool lookInImported = true)
        {
            return FindByPath(identifiers, new Queue<string>(identifiers), lookInImported);
        }

        public ModuleEnvironment? FindByPath(IEnumerable<string> identifiers,
            Queue<string> identifierQueue, bool lookInImported = true)
        {
            // If there are no more sub-modules to find, return the current module
            if (identifierQueue.Count == 0) return this;

            // Start with the first identifier, and make sure the next recursion deals with the next identifier
            var identifier = identifierQueue.Dequeue();
            var moduleList = Modules;

            if (identifier == "root")
            {
                return Root.FindByPath(identifierQueue);
            }

            if (moduleList.TryGetValue(identifier, out ModuleEnvironment? childEnvironment))
            {
                // Proceed to search for the next identifier in the path in the child environment
                return childEnvironment.FindByPath(identifiers, identifierQueue);
            }

            if (_classes.ContainsKey(identifier) ||
                SymbolEnvironment.ContainsFunction(identifier))
            {
                // If the end-symbol has been reached, stop the recursion and return the module environment.
                return this;
            }

            // Couldn't be found
            // Try to find it in the imported modules instead
            if (lookInImported)
            {
                foreach (var import in _importedModules)
                {
                    var module = import.FindByPath(identifiers, false);
                    if (module != null) return module;
                }
            }

            return null;
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