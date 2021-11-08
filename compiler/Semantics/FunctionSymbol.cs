using System;
using System.Collections.Generic;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class FunctionSymbol
    {
        public FunctionDeclStatement Syntax { get; }

        public ICollection<CheckedFunctionDeclStatement> AllChecked => _checkedFunctions.Values;

        public bool HasChecked => _checkedFunctions.Count > 0;

        private readonly Dictionary<string, CheckedFunctionDeclStatement> _checkedFunctions = new();

        public FunctionSymbol(FunctionDeclStatement syntax)
        {
            Syntax = syntax;
        }

        public void AddChecked(CheckedFunctionDeclStatement checkedFunction)
        {
            _checkedFunctions.Add(checkedFunction.FullName, checkedFunction);
        }

        public CheckedFunctionDeclStatement? GetChecked(string fullName)
        {
            _checkedFunctions.TryGetValue(fullName, out CheckedFunctionDeclStatement? result);

            return result;
        }

        public CheckedFunctionDeclStatement? GetCheckedFromClass(CheckedClassDeclStatement checkedClass)
        {
            return GetChecked($"{checkedClass.FullName}.{Syntax.Identifier.Value}");
        }
    }
}