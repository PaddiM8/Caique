using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class StructSymbol
    {
        public ClassDeclStatement Syntax { get; }

        public ICollection<CheckedClassDeclStatement> AllChecked => _checkedClasses.Values;

        public bool HasChecked => _checkedClasses.Count > 0;

        private readonly Dictionary<string, CheckedClassDeclStatement> _checkedClasses = new();

        public StructSymbol(ClassDeclStatement syntax)
        {
            Syntax = syntax;
        }

        public void AddChecked(CheckedClassDeclStatement checkedClass)
        {
            _checkedClasses.Add(checkedClass.FullName, checkedClass);
        }

        public CheckedClassDeclStatement? GetChecked(List<IDataType>? typeArguments = null)
        {
            if (typeArguments == null) return AllChecked.FirstOrDefault();

            _checkedClasses.TryGetValue(
                Syntax.Identifier.Value + SemanticUtils.GetTypeArgumentString(typeArguments),
                out CheckedClassDeclStatement? result
            );

            return result;
        }

        public void TryRemovedChecked(string nameWithExtension)
        {
            _checkedClasses.Remove(nameWithExtension);
        }
    }
}