using System.Collections.Immutable;
using System.Diagnostics;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Analysis;

public class Analyser
{
    private readonly SyntaxTree _syntaxTree;
    private readonly DiagnosticReporter _diagnostics;
    private readonly NamespaceScope _stdScope;
    private readonly TextSpan _blankSpan;
    private readonly CompilerConstantResolver _compilerConstantResolver;

    private Analyser(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
        _stdScope = compilationContext.StdScope;
        _blankSpan = new TextSpan(
            new TextPosition(-1, -1, -1, syntaxTree),
            new TextPosition(-1, -1, -1, syntaxTree)
        );
        _compilerConstantResolver = new CompilerConstantResolver(compilationContext);
    }

    public static SemanticTree Analyse(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        var analyser = new Analyser(syntaxTree, compilationContext);
        Debug.Assert(syntaxTree.Root != null);
        var root = analyser.Next(syntaxTree.Root);
        root.Traverse((node, parent) => node.Parent = parent);

        return new SemanticTree(root, syntaxTree.File);
    }

    private SemanticNode Next(SyntaxNode node)
    {
        Debug.Assert(node.Parent != null || node == _syntaxTree.Root);

        return node switch
        {
            SyntaxStatementNode statementNode => Visit(statementNode),
            SyntaxGroupNode groupNode => Visit(groupNode),
            SyntaxLiteralNode literalNode => Visit(literalNode),
            SyntaxIdentifierNode identifierNode => Visit(identifierNode),
            SyntaxUnaryNode unaryNode => Visit(unaryNode),
            SyntaxBinaryNode binaryNode => Visit(binaryNode),
            SyntaxAssignmentNode assignmentNode => Visit(assignmentNode),
            SyntaxMemberAccessNode memberAccessNode => Visit(memberAccessNode),
            SyntaxCallNode callNode => Visit(callNode),
            SyntaxNewNode newNode => Visit(newNode),
            SyntaxReturnNode returnNode => Visit(returnNode),
            SyntaxKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SyntaxDotKeywordNode dotKeywordNode => Visit(dotKeywordNode),
            SyntaxIfNode ifNode => Visit(ifNode),
            SyntaxBlockNode blockNode => Visit(blockNode),
            SyntaxAttributeNode attributeNode => Visit(attributeNode),
            SyntaxParameterNode parameterNode => Visit(parameterNode),
            SyntaxTypeNode typeNode => Visit(typeNode),
            SyntaxVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SyntaxFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SyntaxClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            SyntaxFieldDeclarationNode fieldDeclarationNode => Visit(fieldDeclarationNode),
            SyntaxInitNode initNode => Visit(initNode),
            SyntaxInitParameterNode initParameterNode => Visit(initParameterNode),
            SyntaxProtocolDeclarationNode protocolDeclarationNode => Visit(protocolDeclarationNode),
            SyntaxModuleDeclarationNode moduleDeclarationNode => Visit(moduleDeclarationNode),
            SyntaxEnumDeclarationNode enumDeclarationNode => Visit(enumDeclarationNode),
            _ => throw new NotImplementedException(),
        };
    }

    private AnalyserRecoveryException Recover()
    {
        return new AnalyserRecoveryException();
    }

    private SemanticKeywordValueNode BuildSelfNode(StructureSymbol structureSymbol)
    {
        var token = new Token(TokenKind.Self, string.Empty, _blankSpan);

        return new SemanticKeywordValueNode(token, [], _blankSpan, new StructureDataType(structureSymbol));
    }

    private SemanticStatementNode Visit(SyntaxStatementNode node)
    {
        var value = Next(node.Expression);

        return new SemanticStatementNode(value);
    }

    private SemanticNode Visit(SyntaxGroupNode node)
    {
        return Next(node.Value);
    }

    private SemanticNode Visit(SyntaxKeywordValueNode node)
    {
        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList();

        return node.Keyword switch
        {
            { Kind: TokenKind.Self } => NextSelfKeyword(node, arguments),
            { Kind: TokenKind.Base } => NextBaseKeyword(node, arguments),
            { Kind: TokenKind.Default } => NextDefaultKeyword(node, arguments),
            { Value: "size_of" } => NextSizeOfKeyword(node, arguments),
            { Value: "get_compiler_constant" } => NextGetCompilerConstantKeyword(node, arguments),
            _ => throw new UnreachableException(),
        };
    }

    private SemanticKeywordValueNode NextSelfKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count > 0)
            _diagnostics.ReportWrongNumberOfArguments(0, arguments.Count, node.Span);

        var function = _syntaxTree.GetEnclosingFunction(node);
        var isWithinMethod = function?.IsStatic is false;
        if (!isWithinMethod)
        {
            _diagnostics.ReportMisplacedSelf(node.Span);
            throw Recover();
        }

        var structure = _syntaxTree.GetEnclosingStructure(node);

        return new SemanticKeywordValueNode(
            node.Keyword,
            null,
            node.Span,
            new StructureDataType(structure!.Symbol!)
        );
    }

    private SemanticKeywordValueNode NextBaseKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        var structure = _syntaxTree.GetEnclosingStructure(node);
        if (arguments == null)
        {
            var function = _syntaxTree.GetEnclosingFunction(node);
            var isWithinMethod = function?.IsStatic is false;
            var isWithinInheritingStructure = structure?
                .SubTypes
                .Any(x => x.ResolvedSymbol?.SyntaxDeclaration is SyntaxClassDeclarationNode)
                is true;

            if (!isWithinMethod || !isWithinInheritingStructure)
            {
                _diagnostics.ReportMisplacedBase(node.Span);
                throw Recover();
            }

            return new SemanticKeywordValueNode(
                node.Keyword,
                null,
                node.Span,
                new StructureDataType(structure!.Symbol!)
            );
        }

        if (node.Parent?.Parent?.Parent is not SyntaxInitNode)
            _diagnostics.ReportMisplacedBaseCall(node.Span);

        var baseClassNode = (structure as SyntaxClassDeclarationNode)?
            .SubTypes
            .FirstOrDefault(x => x.ResolvedSymbol?.SyntaxDeclaration is SyntaxClassDeclarationNode)?
            .ResolvedSymbol?
            .SyntaxDeclaration as SyntaxClassDeclarationNode;

        if (baseClassNode == null)
        {
            _diagnostics.ReportMisplacedBaseCall(node.Span);
            throw Recover();
        }

        Debug.Assert(structure?.Symbol != null);

        var parameterTypes = baseClassNode
            .Init?
            .Parameters
            .Select(Next)
            .Select(x => x.DataType)
            .ToList();
        var typeCheckedArguments = TypeCheckArguments(parameterTypes ?? [], arguments, node.Span);

        return new SemanticKeywordValueNode(
            node.Keyword,
            typeCheckedArguments,
            node.Span,
            new StructureDataType(structure.Symbol)
        );
    }

    private SemanticKeywordValueNode NextDefaultKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count != 1)
        {
            _diagnostics.ReportWrongNumberOfArguments(1, arguments?.Count ?? 0, node.Span);
            throw Recover();
        }

        var dataType = arguments.First().DataType;
        if (arguments.First() is not SemanticTypeNode)
            _diagnostics.ReportExpectedType(arguments.First().Span, got: "value");

        return new SemanticKeywordValueNode(node.Keyword, arguments, node.Span, dataType);
    }

    private SemanticKeywordValueNode NextSizeOfKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count != 1)
        {
            arguments = [];
            _diagnostics.ReportWrongNumberOfArguments(1, arguments.Count, node.Span);
        }

        return new SemanticKeywordValueNode(node.Keyword, arguments, node.Span, new PrimitiveDataType(Primitive.Int64));
    }

    private SemanticLiteralNode NextGetCompilerConstantKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        if (_syntaxTree.GetEnclosingStructure(node)?.Scope.Namespace.ToString().StartsWith("std:") is not true)
        {
            _diagnostics.ReportNotFound(node.Keyword);
            throw Recover();
        }

        if (arguments?.Count != 1)
        {
            arguments = [];
            _diagnostics.ReportWrongNumberOfArguments(1, arguments.Count, node.Span);
        }

        if (arguments.First() is not SemanticLiteralNode { Value.Kind: TokenKind.StringLiteral } stringLiteral)
            throw new InvalidOperationException("Expected string literal as argument to get_compiler_constant.");

        var constant = _compilerConstantResolver.Resolve(stringLiteral.Value.Value, stringLiteral.Value.Span);
        if (constant == null)
        {
            _diagnostics.ReportNotFound(stringLiteral.Value);
            throw Recover();
        }

        return constant;
    }

    private SemanticNode Visit(SyntaxDotKeywordNode node)
    {
        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList();

        if (node.Keyword.Kind == TokenKind.As)
            return NextAsKeyword(node, arguments);

        throw new UnreachableException();
    }

    private SemanticCastNode NextAsKeyword(SyntaxDotKeywordNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count != 1)
        {
            _diagnostics.ReportWrongNumberOfArguments(1, arguments?.Count ?? 0, node.Span);
            throw Recover();
        }

        var value = Next(node.Left);
        var targetDataType = arguments.Single().DataType;

        if (value.DataType is EnumDataType enumValue && (targetDataType is PrimitiveDataType || targetDataType.IsString()))
        {
            var enumTypeNode = enumValue.Symbol.SyntaxDeclaration.Type;
            var enumDataType = enumTypeNode == null
                ? new PrimitiveDataType(Primitive.Int32)
                : Next(enumTypeNode).DataType;
            if (enumDataType.IsEquivalent(targetDataType) == TypeEquivalence.Identical)
                return new SemanticCastNode(value, node.Span, targetDataType);
        }

        if ((value.DataType is PrimitiveDataType || value.DataType.IsString()) && targetDataType is EnumDataType targetEnum)
        {
            var enumTypeNode = targetEnum.Symbol.SyntaxDeclaration.Type;
            var enumDataType = enumTypeNode == null
                ? new PrimitiveDataType(Primitive.Int32)
                : Next(enumTypeNode).DataType;
            if (enumDataType.IsEquivalent(value.DataType) == TypeEquivalence.Identical)
                return new SemanticCastNode(value, node.Span, targetDataType);
        }

        if (value.DataType.GetType() != targetDataType.GetType())
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        if (value.DataType.IsVoid() || targetDataType.IsVoid())
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        if ((value.DataType.IsBoolean() != targetDataType.IsBoolean()))
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        return new SemanticCastNode(value, node.Span, targetDataType);
    }

    private SemanticLiteralNode Visit(SyntaxLiteralNode node)
    {
        if (node.Value.Kind == TokenKind.NumberLiteral)
        {
            var primitiveKind = node.Value.Value.Contains('.')
                ? Primitive.Float32
                : Primitive.Int32;
            var dataType = new PrimitiveDataType(primitiveKind);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        if (node.Value.Kind is TokenKind.True or TokenKind.False)
        {
            var dataType = new PrimitiveDataType(Primitive.Bool);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        if (node.Value.Kind == TokenKind.StringLiteral)
        {
            var symbol = _stdScope.ResolveStructure(["prelude", "String"])!;
            var dataType = new StructureDataType(symbol);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        throw new NotImplementedException();
    }

    private SemanticNode Visit(SyntaxIdentifierNode node)
    {
        if (node.IdentifierList.Count > 1)
        {
            var namespaceNames = node.IdentifierList
                .Take(node.IdentifierList.Count - 1)
                .Select(x => x.Value)
                .ToList();
            var lastIdentifier = node.IdentifierList.Last();

            var foundSymbol = _syntaxTree.File.ResolveSymbol(namespaceNames);
            if (foundSymbol == null)
            {
                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            if (foundSymbol is not StructureSymbol structureSymbol)
            {
                if (foundSymbol is EnumSymbol enumSymbol)
                    return ResolveEnum(enumSymbol, lastIdentifier);

                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            var identifierSymbol = structureSymbol.SyntaxDeclaration.Scope.FindSymbol(lastIdentifier.Value);
            if (identifierSymbol == null)
            {
                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            if (identifierSymbol is FunctionSymbol functionSymbol)
            {
                if (!functionSymbol.SyntaxDeclaration.IsStatic)
                    _diagnostics.ReportNonStaticSymbolReferencedAsStatic(lastIdentifier);

                if (!functionSymbol.SyntaxDeclaration.IsPublic &&
                    structureSymbol.SyntaxDeclaration != _syntaxTree.GetEnclosingStructure(node))
                {
                    _diagnostics.ReportSymbolIsPrivate(lastIdentifier);
                }

                var dataType = new FunctionDataType(functionSymbol);

                return new SemanticFunctionReferenceNode(
                    lastIdentifier,
                    functionSymbol,
                    objectInstance: null,
                    dataType
                );
            }

            if (identifierSymbol is FieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.SyntaxDeclaration.IsStatic)
                    _diagnostics.ReportNonStaticSymbolReferencedAsStatic(lastIdentifier);

                if (!fieldSymbol.SyntaxDeclaration.IsPublic &&
                    structureSymbol.SyntaxDeclaration != _syntaxTree.GetEnclosingStructure(node))
                {
                    _diagnostics.ReportSymbolIsPrivate(lastIdentifier);
                }

                var dataType = Next(fieldSymbol.SyntaxDeclaration.Type).DataType;

                return new SemanticFieldReferenceNode(
                    lastIdentifier,
                    fieldSymbol,
                    objectInstance: null,
                    dataType
                );
            }

            throw new NotImplementedException();
        }

        var identifier = node.IdentifierList.Single();
        if (identifier.Value == "value")
        {
            var keywordNode = TryResolveSetterValueKeywordNode(node);
            if (keywordNode != null)
                return keywordNode;
        }

        var variableSymbol = _syntaxTree.GetLocalScope(node)?.FindSymbol(identifier.Value);
        if (variableSymbol != null)
            return new SemanticVariableReferenceNode(identifier, variableSymbol);

        var structure = _syntaxTree.GetEnclosingStructure(node);
        Debug.Assert(structure?.Symbol != null);

        var symbolFromStructure = structure?.Scope.FindSymbol(identifier.Value);
        if (symbolFromStructure is FunctionSymbol functionSymbol2)
        {
            var dataType = new FunctionDataType(functionSymbol2);
            var instance = functionSymbol2.SyntaxDeclaration.IsStatic
                ? null
                : BuildSelfNode(structure!.Symbol);

            return new SemanticFunctionReferenceNode(identifier, functionSymbol2, instance, dataType);
        }

        if (symbolFromStructure is FieldSymbol fieldSymbol2)
        {
            var dataType = Next(fieldSymbol2.SyntaxDeclaration.Type).DataType;
            var instance = fieldSymbol2.SyntaxDeclaration.IsStatic
                ? null
                : BuildSelfNode(structure!.Symbol);

            Debug.Assert(structure?.Symbol != null);

            // TODO: Verify that the parent function isn't static since you can't reference
            // non-static fields from static functions
            return new SemanticFieldReferenceNode(identifier, fieldSymbol2, instance, dataType);
        }

        var block = _syntaxTree.GetEnclosingBlock(node);
        if (block?.Scope?.Namespace.ResolveSymbol([identifier.Value]) != null)
        {
            _diagnostics.ReportExpectedValueGotType(identifier);

            if (node.Parent is SyntaxMemberAccessNode memberAccess)
                _diagnostics.HintColonInsteadOfDot(memberAccess.Identifier.Span);
        }
        else
        {
            _diagnostics.ReportNotFound(identifier);
        }

        throw Recover();
    }

    private SemanticKeywordValueNode? TryResolveSetterValueKeywordNode(SyntaxIdentifierNode node)
    {
        var identifier = node.IdentifierList.Single();
        var setter = _syntaxTree.GetEnclosingSetter(node);
        if (setter != null)
        {
            var field = (SyntaxFieldDeclarationNode)setter.Parent!;
            var dataType = Next(field.Type).DataType;

            return new SemanticKeywordValueNode(identifier, [], identifier.Span, dataType);
        }

        return null;
    }

    private SemanticEnumReferenceNode ResolveEnum(EnumSymbol symbol, Token memberIdentifier)
    {
        var dataType = new EnumDataType(symbol);
        if (!symbol.SyntaxDeclaration.Members.Any(x => x.Identifier.Value == memberIdentifier.Value))
        {
            _diagnostics.ReportNotFound(memberIdentifier, symbol.SyntaxDeclaration.Identifier.Value);
            throw Recover();
        }

        return new SemanticEnumReferenceNode(memberIdentifier, symbol, dataType);
    }

    private SemanticNode Visit(SyntaxUnaryNode node)
    {
        var value = Next(node.Value);
        if (node.Operator == TokenKind.Exclamation && value.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
        {
            _diagnostics.ReportIncompatibleType(Primitive.Bool, value.DataType, node.Span);
            throw Recover();
        }

        if (node.Operator == TokenKind.Minus && !value.DataType.IsNumber())
        {
            _diagnostics.ReportIncompatibleType("number", value.DataType, node.Span);
            throw Recover();
        }

        return new SemanticUnaryNode(node.Operator, value, value.DataType, node.Span);
    }

    private SemanticNode Visit(SyntaxBinaryNode node)
    {
        var left = Next(node.Left);
        var right = Next(node.Right);
        var dataType = left.DataType;
        if (node.Operator is TokenKind.Plus or TokenKind.Minus or TokenKind.Star or TokenKind.Slash)
        {
            if (!left.DataType.IsNumber())
            {
                _diagnostics.ReportIncompatibleType("number", left.DataType, left.Span);
                throw Recover();
            }

            right = TypeCheck(right, left.DataType);
        }
        else if (node.Operator is TokenKind.AmpersandAmpersand or TokenKind.PipePipe)
        {
            if (left.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
            {
                _diagnostics.ReportIncompatibleType(Primitive.Bool, left.DataType, left.Span);
                throw Recover();
            }

            if (right.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
            {
                _diagnostics.ReportIncompatibleType(Primitive.Bool, right.DataType, right.Span);
                throw Recover();
            }

            dataType = PrimitiveDataType.Bool;

        }
        else if (node.Operator is TokenKind.EqualsEquals or TokenKind.NotEquals)
        {
            var equatable = new StructureDataType(_stdScope.ResolveStructure(["prelude", "Equatable"])!);
            if (left.DataType is not (PrimitiveDataType or EnumDataType) && left.DataType.IsEquivalent(equatable) == TypeEquivalence.Incompatible)
                _diagnostics.ReportIncompatibleType("equatable", left.DataType, left.Span);

            right = TypeCheck(right, left.DataType);
            dataType = PrimitiveDataType.Bool;
        }
        else if (node.Operator is TokenKind.EqualsEqualsEquals or TokenKind.NotEqualsEquals)
        {
            if (left.DataType is not StructureDataType { Symbol.SyntaxDeclaration: ISyntaxReferenceTypeDeclaration })
                _diagnostics.ReportIncompatibleType("reference type", left.DataType, left.Span);

            right = TypeCheck(right, left.DataType);
            dataType = PrimitiveDataType.Bool;
        }
        else if (node.Operator is TokenKind.Greater or TokenKind.GreaterEquals or TokenKind.Less or TokenKind.LessEquals)
        {
            if (!left.DataType.IsNumber())
                _diagnostics.ReportIncompatibleType("number", left.DataType, left.Span);

            right = TypeCheck(right, left.DataType);
            dataType = PrimitiveDataType.Bool;
        }

        return new SemanticBinaryNode(left, node.Operator, right, dataType);
    }

    private SemanticAssignmentNode Visit(SyntaxAssignmentNode node)
    {
        var left = Next(node.Left);
        var right = Next(node.Right);

        ISemanticVariableDeclaration? declaration = null;
        if (left is SemanticVariableReferenceNode variableReference)
        {
            declaration = variableReference.Symbol.SemanticDeclaration;
        }
        else if (left is SemanticFieldReferenceNode fieldReference)
        {
            declaration = fieldReference.Symbol.SemanticDeclaration;
        }
        else
        {
            _diagnostics.ReportExpectedVariableReferenceInAssignment(node.Span);
        }

        if (declaration != null && !declaration.IsMutable)
            _diagnostics.ReportAssignmentToImmutable(node.Span, declaration.Identifier.Span);

        right = TypeCheck(right, left.DataType);

        return new SemanticAssignmentNode(left, right, node.Span);
    }

    private SemanticNode Visit(SyntaxMemberAccessNode node)
    {
        var left = Next(node.Left);
        if (left.DataType is StructureDataType structureDataType)
        {
            var symbol = structureDataType.Symbol.SyntaxDeclaration.ResolveSymbol(node.Identifier.Value);
            if (symbol == null)
            {
                _diagnostics.ReportMemberNotFound(node.Identifier, left.DataType);
                throw Recover();
            }

            if (symbol is FunctionSymbol functionSymbol)
            {
                var dataType = new FunctionDataType(functionSymbol);
                if (node.Parent is not SyntaxCallNode)
                    _diagnostics.ReportNonStaticFunctionReferenceMustBeCalled(node.Identifier);

                if (functionSymbol.SyntaxDeclaration.IsStatic)
                    _diagnostics.ReportStaticSymbolReferencedAsNonStatic(functionSymbol.SyntaxDeclaration.Identifier);

                if (!functionSymbol.SyntaxDeclaration.IsPublic &&
                    structureDataType.Symbol.SyntaxDeclaration != _syntaxTree.GetEnclosingStructure(node))
                {
                    _diagnostics.ReportSymbolIsPrivate(node.Identifier);
                }

                return new SemanticFunctionReferenceNode(
                    functionSymbol.SyntaxDeclaration.Identifier,
                    functionSymbol,
                    objectInstance: left,
                    dataType
                );
            }
            else if (symbol is FieldSymbol fieldSymbol)
            {
                var dataType = Next(fieldSymbol.SyntaxDeclaration.Type).DataType;

                if (fieldSymbol.SyntaxDeclaration.IsStatic)
                    _diagnostics.ReportStaticSymbolReferencedAsNonStatic(fieldSymbol.SyntaxDeclaration.Identifier);

                if (!fieldSymbol.SyntaxDeclaration.IsPublic &&
                    structureDataType.Symbol.SyntaxDeclaration != _syntaxTree.GetEnclosingStructure(node))
                {
                    _diagnostics.ReportSymbolIsPrivate(node.Identifier);
                }

                return new SemanticFieldReferenceNode(
                    fieldSymbol.SyntaxDeclaration.Identifier,
                    fieldSymbol,
                    objectInstance: left,
                    dataType
                );
            }
            else
            {
                throw new UnreachableException();
            }
        }

        _diagnostics.ReportMemberNotFound(node.Identifier, left.DataType);
        throw Recover();
    }

    private SemanticCallNode Visit(SyntaxCallNode node)
    {
        var left = Next(node.Left);
        var arguments = node.Arguments.Select(Next).ToList();

        if (left.DataType is not FunctionDataType functionDataType)
        {
            _diagnostics.ReportIncompatibleType("function reference", left.DataType, left.Span);
            throw Recover();
        }

        var parameterTypes = functionDataType
            .Symbol
            .SyntaxDeclaration
            .Parameters
            .Select(x => Next(x.Type).DataType)
            .ToList();
        var typeCheckedArguments = TypeCheckArguments(parameterTypes, arguments, node.Span);

        var declarationReturnType = functionDataType.Symbol.SyntaxDeclaration.ReturnType;
        var dataType = declarationReturnType == null
            ? PrimitiveDataType.Void
            : Next(declarationReturnType).DataType;

        return new SemanticCallNode(left, typeCheckedArguments, dataType, node.Span);
    }

    private SemanticNewNode Visit(SyntaxNewNode node)
    {
        var dataType = Next(node.Type).DataType;
        if (dataType is not StructureDataType structureDataType)
        {
            _diagnostics.ReportIncompatibleType("name of structure", dataType, node.Span);
            throw Recover();
        }

        SyntaxInitNode? initNode;
        var instantiable = structureDataType.Symbol.SyntaxDeclaration as ISyntaxInstantiableStructureDeclaration;
        if (instantiable != null)
        {
            initNode = instantiable.Init;
        }
        else
        {
            initNode = null;
            _diagnostics.ReportIncompatibleType("name of instantiable structure", dataType, node.Span);
        }

        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();
        var parameterTypes = initNode?
            .Parameters
            .Select(Next)
            .Select(x => x.DataType)
            .ToList()
            ?? [];

        // Will be null if it isn't an instantiable structure (which is an error, so don't try to type check)
        if (instantiable != null)
            arguments = TypeCheckArguments(parameterTypes, arguments, node.Span);

        return new SemanticNewNode(arguments, dataType, node.Span);
    }

    private List<SemanticNode> TypeCheckArguments(List<IDataType> parameterTypes, List<SemanticNode> arguments, TextSpan span)
    {
        if (parameterTypes.Count != arguments.Count)
            _diagnostics.ReportWrongNumberOfArguments(parameterTypes.Count, arguments.Count, span);

        return arguments
            .Zip(parameterTypes)
            .Select(x => TypeCheck(x.First, x.Second))
            .ToList();
    }

    private SemanticNode TypeCheck(SemanticNode value, IDataType targetDataType)
    {
        var equivalence = value.DataType.IsEquivalent(targetDataType);
        if (equivalence == TypeEquivalence.Incompatible)
        {
            if (value is SemanticLiteralNode { Value.Kind: TokenKind.NumberLiteral } literal)
            {
                var primitive = (PrimitiveDataType)value.DataType;
                var isValid = (primitive.IsFloat() && targetDataType.IsFloat()) ||
                    (primitive.IsInteger() && targetDataType.IsFloat()) ||
                    (primitive.IsSignedInteger() && targetDataType.IsSignedInteger()) ||
                    (primitive.IsUnsignedInteger() && targetDataType.IsUnsignedInteger()) ||
                    (primitive.IsSignedInteger() && targetDataType.IsUnsignedInteger());
                if (isValid)
                    return new SemanticLiteralNode(literal.Value, targetDataType);
            }

            _diagnostics.ReportIncompatibleType(targetDataType, value.DataType, value.Span);

            return value;
        }
        else if (equivalence == TypeEquivalence.ImplicitCast)
        {
            return new SemanticCastNode(value, value.Span, targetDataType);
        }

        return value;
    }

    private SemanticReturnNode Visit(SyntaxReturnNode node)
    {
        var value = node.Value == null
            ? null
            : Next(node.Value);

        var enclosingFunction = _syntaxTree.GetEnclosingFunction(node);
        var enclosingGetter = _syntaxTree.GetEnclosingGetter(node);
        SyntaxTypeNode? returnType;
        if (enclosingFunction != null)
        {
            returnType = enclosingFunction?.ReturnType;
        }
        else if (enclosingGetter != null)
        {
            returnType = ((SyntaxFieldDeclarationNode?)enclosingGetter.Parent)?.Type;
        }
        else
        {

            _diagnostics.ReportReturnOutsideFunction(node.Span);
            throw Recover();
        }

        var functionReturnType = returnType == null
            ? null
            : Next(returnType).DataType;
        value = TypeCheckReturnType(functionReturnType, value, node.Span);

        return new SemanticReturnNode(value, node.Span);
    }

    private SemanticNode? TypeCheckReturnType(IDataType? returnType, SemanticNode? value, TextSpan span)
    {
        if (value == null)
        {
            if (returnType != null)
                _diagnostics.ReportIncompatibleType(Primitive.Void, returnType, span);

            return null;
        }

        return TypeCheck(value!, returnType ?? PrimitiveDataType.Void);
    }

    private SemanticIfNode Visit(SyntaxIfNode node)
    {
        var condition = Next(node.Condition);
        var thenBranch = Visit(node.ThenBranch);
        var elseBranch = node.ElseBranch == null
            ? null
            : Visit(node.ElseBranch);

        if (elseBranch != null && thenBranch.DataType.IsEquivalent(elseBranch.DataType) == TypeEquivalence.Incompatible)
            _diagnostics.ReportIncompatibleType(thenBranch.DataType, elseBranch.DataType, node.Span);

        return new SemanticIfNode(condition, thenBranch, elseBranch, node.Span, thenBranch.DataType);
    }

    private SemanticBlockNode Visit(SyntaxBlockNode node)
    {
        var expressions = new List<SemanticNode>();
        foreach (var child in node.Expressions.SkipLast(1))
        {
            if (ShouldSkipBlockChild(child))
                continue;

            try
            {
                expressions.Add(Next(child));
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        IDataType dataType = PrimitiveDataType.Void;
        var last = node.Expressions.LastOrDefault();
        if (last == null || ShouldSkipBlockChild(last))
            return new SemanticBlockNode(expressions, dataType, node.Span);

        SemanticNode? analysedLast;
        try
        {
            analysedLast = Next(last);
        }
        catch (AnalyserRecoveryException)
        {
            return new SemanticBlockNode(expressions, dataType, node.Span);
        }

        if (IsReturnValue(last, analysedLast))
        {
            dataType = analysedLast.DataType;

            SyntaxTypeNode? returnType = null;
            if (node.Parent is ISyntaxFunctionDeclaration enclosingFunction)
            {
                returnType = enclosingFunction.ReturnType;
            }
            else if (node.Parent is SyntaxFieldDeclarationNode enclosingField)
            {
                returnType = node == enclosingField.Getter
                    ? enclosingField.Type
                    : null;
            }

            if (returnType != null)
            {
                var analysedLastStatement = (SemanticStatementNode)analysedLast;
                var functionReturnType = returnType == null
                    ? null
                    : Next(returnType).DataType;
                var typeCheckedValue = TypeCheckReturnType(
                    functionReturnType,
                    analysedLastStatement.Value,
                    node.Span
                );
                var returnNode = new SemanticReturnNode(typeCheckedValue, node.Span);
                analysedLast = new SemanticStatementNode(returnNode);
            }
        }

        expressions.Add(analysedLast);

        return new SemanticBlockNode(expressions, dataType, node.Span);
    }

    private bool IsReturnValue(SyntaxNode syntaxNode, SemanticNode semanticNode)
    {
        if (syntaxNode is SyntaxStatementNode { HasTrailingSemicolon: true })
            return false;

        if (semanticNode is SemanticStatementNode { Value: SemanticBlockNode semanticBlockNode })
            return !semanticBlockNode.DataType.IsVoid();

        if (semanticNode is SemanticStatementNode { Value: SemanticIfNode semanticIfNode })
            return !semanticIfNode.ThenBranch.DataType.IsVoid();

        return true;
    }

    private bool ShouldSkipBlockChild(SyntaxNode child)
    {
        return child is SyntaxErrorNode or SyntaxWithNode;
    }

    private SemanticNode Visit(SyntaxAttributeNode node)
    {
        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();

        return new SemanticAttributeNode(node.Identifier, arguments, node.Span);
    }

    private SemanticNode Visit(SyntaxParameterNode node)
    {
        var dataType = Next(node.Type).DataType;

        return new SemanticParameterNode(node.Identifier, dataType, node.Span);
    }

    private SemanticNode Visit(SyntaxTypeNode node)
    {
        if (node.IsSlice)
        {
            var sliceType = new SliceDataType(NextSimpleType(node));

            return new SemanticTypeNode(sliceType, node.Span);
        }

        return new SemanticTypeNode(NextSimpleType(node), node.Span);
    }

    private IDataType NextSimpleType(SyntaxTypeNode node)
    {
        if (node.TypeNames.Count == 1 && node.TypeNames.First().Value == "Self")
        {
            var parentStructure = _syntaxTree.GetEnclosingStructure(node);
            if (parentStructure == null)
            {
                _diagnostics.ReportMisplacedSelf(node.Span);
                throw Recover();
            }

            return new StructureDataType(parentStructure.Symbol!);
        }

        // Primitive
        Debug.Assert(node.TypeNames.Count > 0);
        var first = node.TypeNames.First();
        if (first.Kind != TokenKind.Identifier)
        {
            var primitiveKind = first.Kind switch
            {
                TokenKind.Void => Primitive.Void,
                TokenKind.Bool => Primitive.Bool,
                TokenKind.I8 => Primitive.Int8,
                TokenKind.I16 => Primitive.Int16,
                TokenKind.I32 => Primitive.Int32,
                TokenKind.I64 => Primitive.Int64,
                TokenKind.I128 => Primitive.Int128,
                TokenKind.ISize => Primitive.ISize,
                TokenKind.U8 => Primitive.Uint8,
                TokenKind.U16 => Primitive.Uint16,
                TokenKind.U32 => Primitive.Uint32,
                TokenKind.U64 => Primitive.Uint64,
                TokenKind.U128 => Primitive.Uint128,
                TokenKind.USize => Primitive.USize,
                TokenKind.F16 => Primitive.Float16,
                TokenKind.F32 => Primitive.Float32,
                TokenKind.F64 => Primitive.Float64,
                _ => throw new NotImplementedException(),
            };

            return new PrimitiveDataType(primitiveKind);
        }

        // Symbol
        var symbol = node.ResolvedSymbol;
        if (node.ResolvedSymbol == null)
        {
            var typeNames = node.TypeNames.Select(x => x.Value).ToList();
            symbol = _syntaxTree.File.ResolveStructure(typeNames);
        }

        if (symbol == null)
        {
            _diagnostics.ReportTypeNotFound(node.TypeNames);
            throw Recover();
        }

        return new StructureDataType(symbol);
    }

    private SemanticNode Visit(SyntaxVariableDeclarationNode node)
    {
        var value = Next(node.Value);
        var dataType = value.DataType;
        if (node.Type != null)
        {
            dataType = Next(node.Type).DataType;
            value = TypeCheck(value, dataType);
        }

        var semanticNode = new SemanticVariableDeclarationNode(
            node.IsMutable,
            node.Identifier,
            value,
            dataType,
            node.Span
        );

        // Variable symbols are created in the analyser, since they can only be referenced
        // after they have been declared
        var scope = _syntaxTree.GetLocalScope(node);
        Debug.Assert(scope != null);
        scope.AddSymbol(new VariableSymbol(semanticNode));

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxFunctionDeclarationNode node)
    {
        var isMain = node.Identifier.Value == "Run" &&
            _syntaxTree.GetEnclosingStructure(node)?.Identifier.Value == "Main";
        if (isMain && !node.IsStatic)
            _diagnostics.ReportNonStaticMainFunction(node.Span);

        var attributes = node
            .Attributes
            .Select(Next)
            .Cast<SemanticAttributeNode>()
            .ToList();

        var parameters = new List<SemanticParameterNode>();
        foreach (var parameter in node.Parameters)
        {
            var semanticParameter = (SemanticParameterNode)Next(parameter);
            parameters.Add(semanticParameter);

            if (node.Body?.Scope is LocalScope scope)
                scope.AddSymbol(new VariableSymbol(semanticParameter));
        }

        IDataType returnType;
        if (node.ReturnType != null)
        {
            returnType = Next(node.ReturnType).DataType;
        }
        else
        {
            returnType = PrimitiveDataType.Void;
        }

        var structure = _syntaxTree.GetEnclosingStructure(node);
        if (node.Identifier.Value == structure?.Identifier.Value)
            _diagnostics.ReportFunctionNameSameAsParentStructure(node.Identifier);

        if (structure != null && node.Identifier.Value == "init")
            _diagnostics.ReportInvalidSymbolName(node.Identifier);

        if (structure is SyntaxProtocolDeclarationNode)
            node.Symbol!.IsVirtual = true;

        var parentClass = structure?
            .SubTypes
            .Select(x => x.ResolvedSymbol?.SyntaxDeclaration as SyntaxClassDeclarationNode)
            .Where(x => x != null)
            .FirstOrDefault();
        if (parentClass != null && node.IsOverride)
        {
            if (!parentClass.IsInheritable)
                _diagnostics.ReportBaseClassNotInheritable(node.Span, parentClass.Span);

            // TODO: If it does not exist in the parent, throw an error.
            // If it does exist, mark the base function as virtual
            var baseFunction = parentClass
                .Declarations
                .FirstOrDefault(x => (x as SyntaxFunctionDeclarationNode)?.Identifier.Value == node.Identifier.Value)
                    as SyntaxFunctionDeclarationNode;
            if (baseFunction == null)
            {
                _diagnostics.ReportBaseFunctionNotFound(node.Identifier);
            }
            else if (baseFunction.Symbol != null)
            {
                baseFunction.Symbol.IsVirtual = true;
            }
        }

        var body = node.Body == null
            ? null
            : (SemanticBlockNode)Next(node.Body);
        var semanticNode = new SemanticFunctionDeclarationNode(
            node.Identifier,
            parameters,
            returnType,
            body,
            node.IsStatic,
            node.IsOverride,
            node.Symbol!,
            node.Span
        )
        {
            Attributes = attributes,
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxClassDeclarationNode node)
    {
        var inheritedClasses = node
            .SubTypes
            .Select(x => x.ResolvedSymbol?.SyntaxDeclaration as SyntaxClassDeclarationNode)
            .Where(x => x != null);

        if (inheritedClasses.Count() > 1)
            _diagnostics.ReportMultipleInheritance(node.Span);

        if (inheritedClasses.FirstOrDefault()?.IsInheritable is false)
            _diagnostics.ReportBaseClassNotInheritable(node.Span, inheritedClasses.First()!.Span);

        StructureSymbol? inheritedClass = null;
        var implementedProtocols = new List<StructureSymbol>();
        foreach (var subTypeNode in node.SubTypes)
        {
            var subTypeDataType = Next(subTypeNode).DataType;
            if (subTypeDataType is StructureDataType structureDataType)
            {
                if (structureDataType.IsClass())
                {
                    inheritedClass = structureDataType.Symbol;
                }
                else if (structureDataType.IsProtocol())
                {
                    implementedProtocols.Add(structureDataType.Symbol);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                _diagnostics.ReportIncompatibleType("class", subTypeDataType, subTypeNode.Span);
            }
        }

        var functions = new List<SemanticFunctionDeclarationNode>();
        var fields = new List<SemanticFieldDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                {
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
                }
                else if (declaration is SyntaxFieldDeclarationNode field)
                {
                    fields.Add((SemanticFieldDeclarationNode)Next(field));
                }
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        SemanticInitNode init;
        if (node.Init == null)
        {
            var emptyBlock = new SemanticBlockNode([], PrimitiveDataType.Void, _blankSpan);
            init = new SemanticInitNode([], null, emptyBlock, _blankSpan);
        }
        else
        {
            init = (SemanticInitNode)Next(node.Init);
        }

        var staticFieldIdentifiers = fields
            .Where(x => x.IsStatic)
            .Select(x => x.Identifier);
        DetectDuplicateEntries(staticFieldIdentifiers);

        var semanticNode = new SemanticClassDeclarationNode(
            node.Identifier,
            inheritedClass,
            implementedProtocols,
            init,
            functions,
            fields,
            node.IsInheritable,
            node.Symbol!,
            node.Span
        )
        {
            FieldStartIndex = CalculateFieldStartIndex(node),
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private void DetectDuplicateEntries(IEnumerable<Token> tokens)
    {
        var touched = new Dictionary<string, Token>();
        foreach (var token in tokens)
        {
            if (touched.TryGetValue(token.Value, out var other))
            {
                _diagnostics.ReportDuplicateEntry(token, other);
            }
            else
            {
                touched[token.Value] = token;
            }
        }
    }

    private static int CalculateFieldStartIndex(SyntaxClassDeclarationNode classDeclarationNode)
    {
        var inheritedClassSyntax = classDeclarationNode.SubTypes
            .Select(x => x.ResolvedSymbol?.SyntaxDeclaration as SyntaxClassDeclarationNode)
            .FirstOrDefault();

        if (inheritedClassSyntax == null)
            return 0;

        var fieldCount = inheritedClassSyntax.Declarations.Count(x => x is SyntaxFieldDeclarationNode);

        return CalculateFieldStartIndex(inheritedClassSyntax) + fieldCount;
    }

    private SemanticFieldDeclarationNode Visit(SyntaxFieldDeclarationNode node)
    {
        if (node.Identifier.Value == "init")
            _diagnostics.ReportInvalidSymbolName(node.Identifier);

        var attributes = node
            .Attributes
            .Select(Next)
            .Cast<SemanticAttributeNode>()
            .ToList();

        var dataType = Next(node.Type).DataType;
        var value = node.Value == null
            ? null
            : Next(node.Value);

        var getter = node.Getter == null
            ? null
            : Visit(node.Getter);
        var setter = node.Setter == null
            ? null
            : Visit(node.Setter);

        if (getter != null && setter == null && node.IsMutable)
            _diagnostics.ReportMutablePropertyWithoutSetter(node.Span);

        if (setter != null && getter == null)
            _diagnostics.ReportSetterButNoGetter(setter.Span);

        if (setter != null && !node.IsMutable)
            _diagnostics.ReportSetterOnImmutable(setter.Span, node.Identifier.Span);

        if (node.IsPublic && node.IsStatic && node.IsMutable)
            _diagnostics.ReportPublicMutableStaticField(node.Span);

        var semanticNode = new SemanticFieldDeclarationNode(
            node.IsMutable,
            node.Identifier,
            value,
            node.IsPublic,
            node.IsStatic,
            dataType,
            node.Symbol!,
            getter,
            setter,
            node.Span
        )
        {
            Attributes = attributes,
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticInitNode Visit(SyntaxInitNode node)
    {
        var analysedParameters = new List<SemanticParameterNode>();
        var assignmentNodes = new List<SemanticAssignmentNode>();
        foreach (var parameter in node.Parameters)
        {
            var analysedParameter = Visit(parameter);
            analysedParameters.Add(analysedParameter);
            var parameterSymbol = new VariableSymbol(analysedParameter);

            var scope = (LocalScope)node.Body.Scope!;
            scope.AddSymbol(parameterSymbol);

            if (parameter.Type != null)
                continue;

            // References a field, so we need to insert assignment nodes
            var structure = _syntaxTree.GetEnclosingStructure(node);
            Debug.Assert(structure?.Symbol != null);
            var linkedSymbol = (FieldSymbol)structure.Scope.FindSymbol(parameter.Identifier.Value)!;
            var left = new SemanticFieldReferenceNode(
                parameter.Identifier,
                linkedSymbol,
                objectInstance: BuildSelfNode(structure.Symbol),
                analysedParameter.DataType
            );

            var right = new SemanticVariableReferenceNode(parameter.Identifier, parameterSymbol);
            var assignmentNode = new SemanticAssignmentNode(left, right, _blankSpan);
            assignmentNodes.Add(assignmentNode);
        }

        var body = (SemanticBlockNode)Next(node.Body);
        body.Expressions.AddRange(assignmentNodes);

        var baseCall = body.Expressions.FirstOrDefault() is SemanticKeywordValueNode { Keyword.Kind: TokenKind.Base } baseCallNode
            ? baseCallNode
            : null;
        if (baseCall != null)
            body.Expressions.RemoveAt(0);

        foreach (var expression in body.Expressions.Skip(1))
        {
            if (expression is SemanticKeywordValueNode { Keyword.Kind: TokenKind.Base } baseNode)
                _diagnostics.ReportMisplacedBaseCall(baseNode.Span);
        }

        return new SemanticInitNode(analysedParameters, baseCall, body, node.Span);
    }

    private SemanticParameterNode Visit(SyntaxInitParameterNode node)
    {
        if (node.Type != null)
        {
            var dataType = Next(node.Type).DataType;

            return new SemanticParameterNode(node.Identifier, dataType, node.Span);
        }

        Debug.Assert(node.LinkedField != null);
        var linkedDataType = Next(node.LinkedField.Type).DataType;

        return new SemanticParameterNode(node.Identifier, linkedDataType, node.Span);
    }

    private SemanticProtocolDeclarationNode Visit(SyntaxProtocolDeclarationNode node)
    {
        var functions = new List<SemanticFunctionDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        var semanticNode = new SemanticProtocolDeclarationNode(
            node.Identifier,
            functions,
            node.Symbol!,
            node.Span
        );

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticModuleDeclarationNode Visit(SyntaxModuleDeclarationNode node)
    {
        var functions = new List<SemanticFunctionDeclarationNode>();
        var fields = new List<SemanticFieldDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                {
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
                }
                else if (declaration is SyntaxFieldDeclarationNode field)
                {
                    fields.Add((SemanticFieldDeclarationNode)Next(field));
                }
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        DetectDuplicateEntries(functions.Select(x => x.Identifier));
        DetectDuplicateEntries(fields.Select(x => x.Identifier));

        var semanticNode = new SemanticModuleDeclarationNode(
            node.Identifier,
            functions,
            fields,
            node.Symbol!,
            node.Span
        );

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticEnumDeclarationNode Visit(SyntaxEnumDeclarationNode node)
    {
        var members = new List<SemanticEnumMemberNode>();
        var memberDataType = node.Type == null
            ? new PrimitiveDataType(Primitive.Int32)
            : Next(node.Type).DataType;

        if (memberDataType is not PrimitiveDataType && !memberDataType.IsString())
        {
            _diagnostics.ReportInvalidEnumType(memberDataType, node.Span);
            throw Recover();
        }

        SemanticLiteralNode? lastValue = null;
        foreach (var member in node.Members)
        {
            try
            {
                var analysedMember = Visit(member, node, memberDataType, lastValue);
                members.Add(analysedMember);
                lastValue = analysedMember.Value;
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        DetectDuplicateEntries(members.Select(x => x.Identifier));

        var semanticNode = new SemanticEnumDeclarationNode(
            node.Identifier,
            members,
            memberDataType,
            node.Symbol!,
            node.Span
        );

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticEnumMemberNode Visit(
        SyntaxEnumMemberNode node,
        SyntaxEnumDeclarationNode declaration,
        IDataType targetDataType,
        SemanticLiteralNode? lastValue
    )
    {
        SemanticNode value;
        if (node.Value == null && targetDataType.IsInteger())
        {
            var oneToken = new Token(TokenKind.NumberLiteral, "1", _blankSpan);
            var intDataType = new PrimitiveDataType(Primitive.Int32);
            var oneLiteral = new SemanticLiteralNode(oneToken, intDataType);
            if (lastValue == null)
            {
                var zeroToken = oneToken with { Value = "0" };
                value = new SemanticLiteralNode(zeroToken, intDataType);
            }
            else
            {
                value = new SemanticBinaryNode(lastValue, TokenKind.Plus, oneLiteral, intDataType);
            }
        }
        else if (node.Value == null && targetDataType.IsString())
        {
            var stringToken = new Token(TokenKind.StringLiteral, node.Identifier.Value, _blankSpan);
            var stringDataType = new StructureDataType(_stdScope.ResolveStructure(["prelude", "String"]!)!);
            value = new SemanticLiteralNode(stringToken, stringDataType);
        }
        else if (node.Value == null)
        {
            _diagnostics.ReportInvalidEnumMemberValue($"Expected explicit value for type {targetDataType}", node.Span);
            throw Recover();
        }
        else
        {
            value = Next(node.Value);
        }

        try
        {
            var folder = new ConstantFolder(TypeCheck);
            var foldedValue = folder.Fold(value, targetDataType);

            return new SemanticEnumMemberNode(
                node.Identifier,
                foldedValue,
                new EnumDataType(declaration.Symbol!),
                node.Span
            );
        }
        catch (Exception ex)
        {
            _diagnostics.ReportInvalidEnumMemberValue(ex.Message, node.Span);
            throw Recover();
        }
    }

}
