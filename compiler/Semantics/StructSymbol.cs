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

        public CheckedClassDeclStatement? GetChecked(string fullName)
        {
            _checkedClasses.TryGetValue(fullName, out CheckedClassDeclStatement? result);

            return result;
        }

        public CheckedClassDeclStatement? GetCheckedFromTypeArguments(List<IDataType> typeArguments)
        {
            string typeArgumentString = string.Join(",", typeArguments.Select(x => x.ToString()));
            string fullName = $"{Syntax.Identifier.Value}[{typeArgumentString}]";

            return GetChecked(fullName);
        }
    }
}