using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Tree structure of modules.
    /// Classes will be stored inside "directory" modules,
    /// while functions are stored inside "file" modules.
    /// If you define a class inside a file, it will be stored
    /// in that file's parent module (which is a directory).
    /// </summary>
    public class ModuleEnvironment
    {
        public string Identifier { get; }

        /// <summary>
        /// The path to the file associated with the module.
        /// If it's a directory, the value is null.
        /// </summary>
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
            if (Parent != null) ImportModule(Parent);
        }

        /// <summary>
        /// Create a new module inside this one.
        /// </summary>
        /// <param name="identifier">The name of the module.</param>
        /// <param name="filePath">The path to the file associated with the module (null if it's a directory).</param>
        /// <returns></returns>
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

        /// <summary>
        /// Import a module, so that it will be looked in when trying to find
        /// symbols.
        /// </summary>
        /// <param name="module"></param>
        public void ImportModule(ModuleEnvironment module)
        {
            _importedModules.Add(module);
        }

        /// <summary>
        /// Get a class from the current module or directly from
        /// one of its imported modules.
        /// </summary>
        /// <param name="identifier">Name of the class.</param>
        /// <param name="lookInImports">Whether or not to try to find it in imported modules.</param>
        /// <returns>Null if none was found.</returns>
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

        /// <summary>
        /// Get a class from a module path.
        /// </summary>
        /// <param name="modulePath">Module path.</param>
        /// <returns>Null if none was found.</returns>
        public ClassDeclStatement? GetClass(List<Token> modulePath)
        {
            string identifier = modulePath[^1].Value;

            return modulePath.Count > 1
                ? FindByPath(modulePath)?.GetClass(identifier, false)
                : GetClass(identifier);
        }

        /// <summary>
        /// Get a function from the current module or directly from
        /// one of its imported modules.
        /// </summary>
        /// <param name="identifier">Name of the function.</param>
        /// <param name="lookInImports">Whether or not to try to find it in imported modules.</param>
        /// <returns>Null if none was found.</returns>
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

        /// <summary>
        /// Find another module using a module path (list of tokens).
        /// If the last token in the module path is a class/function/etc.
        /// it will try to reach that, and will return the module it is in.
        /// </summary>
        /// <param name="identifiers">Module path.</param>
        /// <param name="lookInImports">Whether or not to look in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public ModuleEnvironment? FindByPath(IEnumerable<Token> identifiers, bool lookInImported = true)
        {
            var values = identifiers.Select(x => x.Value);

            return FindByPath(values, new Queue<string>(values), lookInImported);
        }

        /// <summary>
        /// Find another module using a module path (list of tokens).
        /// If the last token in the module path is a class/function/etc.
        /// it will try to reach that, and will return the module it is in.
        /// </summary>
        /// <param name="identifiers">Module path.</param>
        /// <param name="lookInImports">Whether or not to look in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public ModuleEnvironment? FindByPath(IEnumerable<string> identifiers, bool lookInImported = true)
        {
            return FindByPath(identifiers, new Queue<string>(identifiers), lookInImported);
        }

        private ModuleEnvironment? FindByPath(IEnumerable<string> identifiers,
            Queue<string> identifierQueue, bool lookInImported = true)
        {
            // If there are no more sub-modules to find, return the current module
            if (identifierQueue.Count == 0) return this;

            // Start with the first identifier, and make sure the next recursion deals with the next identifier
            var identifier = identifierQueue.Dequeue();

            if (identifier == "root")
            {
                return Root.FindByPath(identifierQueue);
            }

            if (Modules.TryGetValue(identifier, out ModuleEnvironment? childEnvironment))
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