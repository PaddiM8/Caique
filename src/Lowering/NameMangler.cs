using System.Text;
using Caique.Analysis;

namespace Caique.Lowering;

class NameMangler(TypeArgumentResolver typeArgumentResolver)
{
    private readonly TypeArgumentResolver _typeArgumentResolver = typeArgumentResolver;

    public string BuildTypeName(IDataType dataType)
    {
        if (dataType is StructureDataType structureDataType)
            return structureDataType.QualifiedName + MangleTypeArgumentList(structureDataType.TypeArguments);

        return dataType.ToString()!;
    }

    private string MangleTypeArgumentList(IEnumerable<IDataType> typeArguments)
    {
        var mangledTypeArguments = new List<string>();
        foreach (var typeArgument in typeArguments)
        {
            if (typeArgument is TypeParameterDataType typeParameterDataType)
            {
                var mangledType = BuildTypeName(_typeArgumentResolver.Resolve(typeParameterDataType.Symbol));
                mangledTypeArguments.Add(mangledType);
            }
            else
            {
                mangledTypeArguments.Add(BuildTypeName(typeArgument));
            }
        }

        if (mangledTypeArguments.Count == 0)
            return string.Empty;

        return $"[{string.Join(", ", mangledTypeArguments)}]";
    }

    public string BuildStaticFieldName(SemanticFieldDeclarationNode field)
    {
        var ffiAttribute = field.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        var parentStructure = SemanticTree.GetEnclosingStructure(field);
        var structureName = BuildStructName(parentStructure!, typeArguments: []);

        return ffiAttribute == null
            ? $"{structureName}:{field.Identifier.Value}"
            : field.Identifier.Value;
    }

    public string BuildFunctionName(
        SemanticFunctionDeclarationNode function,
        List<IDataType> typeArguments,
        List<IDataType> structureTypeArguments
    )
    {
        var parentStructure = SemanticTree.GetEnclosingStructure(function);
        var structureName = BuildStructName(parentStructure!, structureTypeArguments);

        return BuildFunctionNameWithStructName(function, typeArguments, structureName);
    }

    public string BuildUnqualifiedFunctionName(SemanticFunctionDeclarationNode function, List<IDataType> typeArguments)
    {
        var parentStructure = SemanticTree.GetEnclosingStructure(function);
        var generics = string.Empty;
        if (typeArguments.Count > 0)
        {
            var builtTypeArguments = typeArguments.Select(BuildTypeName);
            generics = $"[{string.Join(", ", builtTypeArguments)}]";
        }

        return function.Identifier.Value + generics;
    }

    public string BuildFunctionNameWithStructName(
        SemanticFunctionDeclarationNode function,
        List<IDataType> typeArguments,
        string structName
    )
    {
        var ffiAttribute = function.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
            return function.Identifier.Value;

        var unqualifiedName = BuildUnqualifiedFunctionName(function, typeArguments);

        return $"{structName}:{unqualifiedName}";
    }

    public string BuildStructName(ISemanticStructureDeclaration structure, List<IDataType> typeArguments)
    {
        var ffiAttribute = structure.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
            return structure.Identifier.Value;

        var dataType = new StructureDataType(structure.Symbol, typeArguments);

        return BuildTypeName(dataType);
    }

    public string BuildConstructorName(ISemanticStructureDeclaration structure, List<IDataType> structureTypeArguments)
    {
        var structureName = BuildStructName(structure, structureTypeArguments);

        return $"{structureName}:init";
    }

    public string BuildGetterName(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        return $"{BuildPropertyBaseName(field, structureTypeArguments)}.get";
    }

    public string BuildSetterName(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        return $"{BuildPropertyBaseName(field, structureTypeArguments)}.set";
    }

    public string BuildPropertyBaseName(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        var ffiAttribute = field.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
            return field.Identifier.Value;

        var parentStructure = SemanticTree.GetEnclosingStructure(field);
        var structureName = BuildStructName(parentStructure!, structureTypeArguments);

        return $"{structureName}:{field.Identifier.Value}";
    }

    public string BuildVtableName(StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        var implementorName = BuildTypeName(implementorDataType);
        var implementedName = BuildTypeName(implementedDataType);

        return $"{implementorName}.vtable.{implementedName}";
    }

    public string BuildTypeTableName(StructureDataType structureDataType)
    {
        return $"typetable.{BuildTypeName(structureDataType)}";
    }
}
