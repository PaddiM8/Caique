using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.CodeGeneration;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    /// <summary>
    /// Tree structure of modules.
    /// Each module belongs to a file,
    /// and compilation of files is done as they're needed.
    /// If one file uses something from another module,
    /// that module will be compiled first unless it hasn't been already.
    /// </summary>
    public class ModuleEnvironment
    {
        /// <summary>
        /// Name of the module.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// The path to the file/directory associated with the module.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// The path to the file/directory relative to the root module.
        /// </summary>
        public string RelativeFilePath => Path.GetRelativePath(Root.FilePath, FilePath);

        public ModuleEnvironment? Parent;

        public ModuleEnvironment Root { get; }

        public SymbolEnvironment SymbolEnvironment { get; } =
            new SymbolEnvironment();

        /// <summary>
        /// Whether or not the module contains code, or is just a directory module.
        /// </summary>
        public bool IsCodeModule { get; }

        public List<Token>? Tokens { get; private set; }

        public IEnumerable<Statement>? Ast { get; private set; }

        public IEnumerable<CheckedStatement>? TypeTree { get; private set; }

        public DiagnosticBag Diagnostics { get; }

        public List<ModuleEnvironment> ImportedModules { get; private set; }
            = new List<ModuleEnvironment>();

        public Dictionary<string, ModuleEnvironment> Modules { get; } = new();

        public ModuleEnvironment? Prelude { get; }

        private readonly string _outputDirectory;
        private readonly Dictionary<string, string> _libraryPaths;

        private LlvmGenerator? _codeGenerator;

        /// <summary>
        /// Create a child module
        /// </summary>
        public ModuleEnvironment(string identifier,
                                 string filePath,
                                 ModuleEnvironment parent)
        {
            Identifier = identifier;
            FilePath = filePath;
            Parent = parent;
            Root = Parent.Root;
            IsCodeModule = filePath!.EndsWith(".cq");
            _outputDirectory = Parent._outputDirectory;
            _libraryPaths = Root._libraryPaths;
            Diagnostics = Root.Diagnostics;
            Prelude = Parent.Prelude;

            if (IsCodeModule)
            {
                ImportModule(Parent);

                if (Prelude != null)
                {
                    ImportedModules.AddRange(Prelude.Modules.Values);
                }

                string previousDiagnosticsFile = Diagnostics.CurrentFile;
                Diagnostics.CurrentFile = RelativeFilePath;

                // Lexing and parsing
                Tokens = Lexer.Lex(File.ReadAllText(filePath), Diagnostics);
                Ast = Parser.Parse(Tokens, Diagnostics, this);

                // Type checking
                TypeTree = TypeChecker.Analyse(this);

                Diagnostics.CurrentFile = previousDiagnosticsFile;
            }
        }

        /// <summary>
        /// Create a root module
        /// </summary>
        public ModuleEnvironment(string identifier,
                                 string filePath,
                                 string outputDirectory,
                                 Dictionary<string, string> libraryPaths,
                                 DiagnosticBag diagnostics,
                                 ModuleEnvironment? prelude)
        {
            Identifier = identifier;
            FilePath = filePath;
            Root = this;
            IsCodeModule = false;
            _outputDirectory = outputDirectory;
            _libraryPaths = libraryPaths;
            Diagnostics = diagnostics;
            Prelude = prelude;
        }

        public void GenerateSymbols()
        {
            if (Diagnostics.Any()) return;

            if (IsCodeModule)
            {
                _codeGenerator = new LlvmGenerator(this);
                _codeGenerator.GenerateSymbols();
            }

            foreach (var child in Modules.Values)
            {
                child.GenerateSymbols();
            }
        }

        public void GenerateContent()
        {
            if (Diagnostics.Any()) return;

            if (IsCodeModule)
            {
                _codeGenerator!.GenerateContent();
            }

            foreach (var child in Modules.Values)
            {
                child.GenerateContent();
            }
        }

        public void Emit(OutputKind outputType)
        {
            if (Diagnostics.Any()) return;

            if (IsCodeModule)
            {
                if (outputType == OutputKind.ObjectFile)
                {
                    _codeGenerator!.GenerateObjectFile(_outputDirectory);
                }
                else if (outputType == OutputKind.IntermediateRepresentation)
                {
                    _codeGenerator!.GenerateLlvmFile(_outputDirectory);
                }
            }

            foreach (var child in Modules.Values)
            {
                child.Emit(outputType);
            }
        }

        /// <summary>
        /// Create a new module inside this one.
        /// </summary>
        /// <param name="identifier">The name of the module.</param>
        /// <param name="filePath">The path to the file associated with the module (null if it's a directory).</param>
        /// <returns></returns>
        public ModuleEnvironment CreateChildModule(string identifier, string filePath)
        {
            var module = new ModuleEnvironment(identifier, filePath, this);
            Modules[identifier] = module;

            return module;
        }

        /// <summary>
        /// Ensures every module in the module path has been created.
        /// </summary>
        /// <param name="modulePath">List of module names</param>
        /// <returns>The module for the last name in the list.</returns>
        private ModuleEnvironment? CreateModulesFromModulePath(IEnumerable<string> modulePath)
        {
            string filePath;
            ModuleEnvironment module;
            if (modulePath.First() == "root") // Start from root
            {
                filePath = Root.FilePath;
                module = Root;
                modulePath = modulePath.Skip(1);
            }
            else if (_libraryPaths.TryGetValue(modulePath.First(), out string? libraryPath)) // Library
            {
                filePath = Path.Combine(libraryPath, "src");
                module = Root;
                modulePath = modulePath.Skip(1);
            }
            else // Normal, relative module
            {
                filePath = FilePath;
                module = this;
            }

            int i = 0;
            int lastIndex = modulePath.Count() - 1; // TODO: Optimise
            ModuleEnvironment lastModule = module;
            foreach (var moduleName in modulePath)
            {
                filePath += "/" + moduleName;

                // If the last one is not a directory
                if (i == lastIndex && !Directory.Exists(filePath))
                {
                    filePath += ".cq";
                    if (!File.Exists(filePath)) return null;
                    var parent = lastModule ?? module; // Create a child in the previous module in the list

                    return parent.CreateChildModule(moduleName, filePath);
                }

                // Create "directory" module, that can have children.
                lastModule = module.CreateChildModule(moduleName, filePath);
                i++;
            }

            // Make sure the module exists in the project directory
            if (Directory.Exists(filePath)) return null;

            return lastModule;
        }

        /// <summary>
        /// Import a module, so that it will be looked in when trying to find
        /// symbols.
        /// </summary>
        /// <param name="module"></param>
        public void ImportModule(ModuleEnvironment module)
        {
            ImportedModules.Add(module);
        }

        /// <summary>
        /// Get a function from the current module or directly from
        /// one of its imported modules.
        /// </summary>
        /// <param name="identifier">Name of the function.</param>
        /// <param name="lookInImports">Whether or not to try to find it in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public StructSymbol? GetClass(string identifier, bool lookInImports = true)
        {
            var classDecl = SymbolEnvironment.GetClass(identifier);
            if (classDecl != null)
            {
                return classDecl;
            }
            else if (lookInImports)
            {
                // Try to find it in the imports
                foreach (var import in ImportedModules)
                {
                    var foundClass = import.GetClass(identifier, false);
                    if (foundClass != null) return foundClass;
                }
            }

            return null;
        }

        /// <summary>
        /// Get a function from the current module or directly from
        /// one of its imported modules.
        /// </summary>
        /// <param name="identifier">Name of the function.</param>
        /// <param name="lookInImports">Whether or not to try to find it in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public FunctionSymbol? GetFunction(string identifier, bool lookInImports = true)
        {
            var functionDecl = SymbolEnvironment.GetFunction(identifier);
            if (functionDecl != null)
            {
                return functionDecl;
            }
            else if (lookInImports)
            {
                // Try to find it in the imports
                foreach (var import in ImportedModules)
                {
                    var foundFunction = import.GetFunction(identifier, false);
                    if (foundFunction != null) return foundFunction;
                }
            }

            return null;
        }

        /// <summary>
        /// Find another module using a module path (list of tokens).
        /// </summary>
        /// <param name="modulePath">List of module names in order. Eg. { "animals", "duck" }</param>
        /// <param name="lookInImports">Whether or not to look in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public ModuleEnvironment? FindByPath(IEnumerable<Token> modulePath)
        {
            var values = modulePath.Select(x => x.Value);

            return FindByPath(values);
        }

        /// <summary>
        /// Find another module using a module path (list of tokens).
        /// </summary>
        /// <param name="modulePath">List of module names in order. Eg. { "animals", "duck" }</param>
        /// <param name="lookInImports">Whether or not to look in imported modules.</param>
        /// <returns>Null if none was found.</returns>
        public ModuleEnvironment? FindByPath(IEnumerable<string> modulePath)
        {
            var (lastFound, notFoundPart) = FindByPath(
                modulePath,
                new Queue<string>(modulePath)
            );

            // If every module in the path was found, return the result
            if (notFoundPart == null) return lastFound;
            lastFound ??= this; // If no module was found, use the current one

            return lastFound.CreateModulesFromModulePath(notFoundPart!);
        }

        private (ModuleEnvironment? lastFound, IEnumerable<string>? notFoundPart)
            FindByPath(IEnumerable<string> modulePath, Queue<string> modulePathQueue)
        {
            // If there are no more sub-modules to find, return the current module
            if (modulePathQueue.Count == 0) return (this, null);

            // Start with the first identifier,
            // and make sure the next recursion deals with the next identifier
            var identifier = modulePathQueue.Dequeue();

            if (identifier == "root")
            {
                return Root.FindByPath(modulePathQueue, modulePathQueue);
            }

            if (Modules.TryGetValue(identifier, out ModuleEnvironment? childEnvironment))
            {
                // Proceed to search for the next identifier in the path in the child environment
                var next = childEnvironment.FindByPath(modulePath, modulePathQueue);
                if (next.lastFound == null) return (childEnvironment, next.notFoundPart);
                else return next;
            }

            return (null, modulePathQueue.Prepend(identifier));
        }
    }
}