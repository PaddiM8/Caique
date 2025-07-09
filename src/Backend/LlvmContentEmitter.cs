using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Caique.Analysis;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmContentEmitter
{
    private readonly SemanticTree _semanticTree;
    private readonly LLVMContextRef _context;
    private readonly LLVMBuilderRef _builder;
    private readonly LLVMModuleRef _module;
    private readonly LlvmTypeBuilder _typeBuilder;
    private readonly LlvmContextCache _contextCache;
    private readonly LlvmModuleCache _moduleCache;
    private readonly NamespaceScope _preludeScope;
    private readonly LlvmSpecialValueBuilder _specialValueBuilder;
    private readonly LLVMDIBuilderRef _diBuilder;
    private readonly LLVMMetadataRef _compileUnit;
    private readonly LLVMMetadataRef _diFile;
    private LLVMMetadataRef? _currentFunctionMetadata;

    private LlvmContentEmitter(SemanticTree semanticTree, LlvmEmitterContext emitterContext, CompilationContext compilationContext)
    {
        _semanticTree = semanticTree;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _contextCache = emitterContext.ContextCache;
        _moduleCache = emitterContext.ModuleCache;
        _preludeScope = compilationContext.PreludeScope;
        _specialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _typeBuilder);
        var (diBuilder, compileUnit, diFile) = SetUpDiBuilder(_context, _module, semanticTree.Root.Span.Start.SyntaxTree.File.FilePath);
        _diBuilder = diBuilder;
        _compileUnit = compileUnit;
        _diFile = diFile;
    }

    public static string Emit(
        SemanticTree semanticTree,
        LlvmEmitterContext emitterContext,
        string targetDirectory,
        CompilationContext compilationContext,
        CompilationOptions? compilationOptions = null
    )
    {
        var emitter = new LlvmContentEmitter(semanticTree, emitterContext, compilationContext);
        emitter.Next(semanticTree.Root);

        if (compilationOptions?.DumpIr is true)
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, "ir"));
            var dumpFilePath = Path.Combine(targetDirectory, "ir", $"{emitterContext.ModuleName}.ll");
            emitter._module.PrintToFile(dumpFilePath);
        }

        Directory.CreateDirectory(Path.Combine(targetDirectory, "objects"));
        var objectPath = Path.Combine(targetDirectory, "objects", $"{emitterContext.ModuleName}.o");

        var isValid = emitter._module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out var errorMessage);
        if (isValid)
        {
            emitter.CreateObjectFile(objectPath);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Compilation error in module {emitterContext.ModuleName}:");
            Console.ResetColor();
            Console.Write("  ");
            Console.Error.WriteLine(errorMessage);
        }

        return objectPath;
    }

    private static (LLVMDIBuilderRef, LLVMMetadataRef, LLVMMetadataRef) SetUpDiBuilder(
        LLVMContextRef context,
        LLVMModuleRef module,
        string sourcePath
    )
    {
        var metadata = LLVMValueRef.CreateConstInt(context.Int32Type, 3);
        module.AddNamedMetadataOperand("Debug Info Version", context.GetMDNode([metadata]));

        var diBuilder = module.CreateDIBuilder();
        var file = diBuilder.CreateFile(Path.GetFileName(sourcePath), Path.GetDirectoryName(sourcePath)!);
        var compileUnit = diBuilder.CreateCompileUnit(
            SourceLanguage: LLVMDWARFSourceLanguage.LLVMDWARFSourceLanguageC,
            FileMetadata: file,
            Producer: "Caique",
            IsOptimized: 0,
            Flags: [],
            RuntimeVersion: 0,
            SplitName: string.Empty,
            DwarfEmissionKind: LLVMDWARFEmissionKind.LLVMDWARFEmissionFull,
            DWOld: 0,
            SplitDebugInlining: 0,
            DebugInfoForProfiling: 0,
            SysRoot: [],
            SDK: string.Empty
        );

        return (diBuilder, compileUnit, file);
    }

    private void CreateObjectFile(string path)
    {
        LLVM.InitializeNativeTarget();
        LLVM.InitializeNativeAsmPrinter();

        var target = LLVMTargetRef.GetTargetFromTriple(LLVMTargetRef.DefaultTriple);
        string hostCpu;
        string hostCpuFeatures;
        unsafe
        {
            hostCpu = Marshal.PtrToStringAnsi((IntPtr)LLVM.GetHostCPUName())!;
            hostCpuFeatures = Marshal.PtrToStringAnsi((IntPtr)LLVM.GetHostCPUFeatures())!;
        }

        var targetMachine = target.CreateTargetMachine(
            LLVMTargetRef.DefaultTriple,
            hostCpu,
            hostCpuFeatures,
            LLVMCodeGenOptLevel.LLVMCodeGenLevelNone,
            LLVMRelocMode.LLVMRelocDefault,
            LLVMCodeModel.LLVMCodeModelDefault
        );

        _diBuilder.DIBuilderFinalize();

        var success = targetMachine.TryEmitToFile(_module, path, LLVMCodeGenFileType.LLVMObjectFile, out var error);
        if (!success)
            throw new Exception(error);
    }

    private LLVMValueRef? Next(SemanticNode node)
    {
        Debug.Assert(node.Parent != null || node == _semanticTree.Root);

        return node switch
        {
            SemanticStatementNode statementNode => Visit(statementNode),
            SemanticLiteralNode literalNode => Visit(literalNode),
            SemanticVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            SemanticFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            SemanticFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            SemanticEnumReferenceNode enumReferenceNode => Visit(enumReferenceNode),
            SemanticUnaryNode unaryNode => Visit(unaryNode),
            SemanticBinaryNode binaryNode => Visit(binaryNode),
            SemanticAssignmentNode assignmentNode => Visit(assignmentNode),
            SemanticCallNode callNode => Visit(callNode),
            SemanticNewNode newNode => Visit(newNode),
            SemanticReturnNode returnNode => Visit(returnNode),
            SemanticKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SemanticCastNode castNode => Visit(castNode),
            SemanticBlockNode blockNode => Visit(blockNode),
            SemanticVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SemanticFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SemanticClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            SemanticProtocolDeclarationNode => null,
            SemanticModuleDeclarationNode moduleDeclarationNode => Visit(moduleDeclarationNode),
            SemanticEnumDeclarationNode => null,
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef GetSelf()
    {
        return _builder.InsertBlock.Parent.GetParam(0);
    }

    private LLVMValueRef BuildString(string value)
    {
        // var stringType = LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, (uint)Encoding.Default.GetByteCount(value));
        /*var indices = new LLVMValueRef[]*/
        /*{*/
        /*    LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 0, false),*/
        /*    LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 0, false),*/
        /*};*/
        /**/
        /*var valuePointer = _builder.BuildGEP2(stringType, globalString, indices);*/

        var symbol = _preludeScope.ResolveStructure(["String"])!;
        var type = _typeBuilder.BuildNamedStructType(new StructureDataType(symbol.SemanticDeclaration!.Symbol));
        var instance = _specialValueBuilder.BuildMalloc(type, "str");
        var structure = (ISemanticInstantiableStructureDeclaration)symbol.SemanticDeclaration;

        var global = _builder.BuildGlobalString(value);
        var valuePointer = _builder.BuildBitCast(global, LLVMTypeRef.CreatePointer(_context.Int8Type, 0));
        BuildConstructorCall(instance, structure, [valuePointer]);

        return instance;
    }

    private LLVMValueRef? Visit(SemanticStatementNode node)
    {
        var value = Next(node.Value);

        if (_currentFunctionMetadata.HasValue)
        {
            var debugLocation = _context.CreateDebugLocation(
                Line: (uint)node.Span.Start.Line + 1,
                Column: (uint)node.Span.Start.Column + 1,
                Scope: _currentFunctionMetadata.Value,
                InlinedAt: default
            );

            unsafe
            {
                LLVM.SetCurrentDebugLocation2(_builder, debugLocation);
            }
        }

        return value;
    }

    private LLVMValueRef Visit(SemanticLiteralNode node)
    {
        if (node.DataType is StructureDataType { Symbol.Name: "String" })
            return BuildString(node.Value.Value);

        var primitive = (PrimitiveDataType)node.DataType;
        return primitive.Kind switch
        {
            Primitive.Void => throw new InvalidOperationException(),
            Primitive.Bool => LlvmUtils.CreateConstBool(_context, node.Value.Kind == TokenKind.True),
            >= Primitive.Int8 and <= Primitive.Int128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: true
            ),
            >= Primitive.Uint8 and <= Primitive.Uint128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: false
            ),
            >= Primitive.Float16 and <= Primitive.Float64 => LLVMValueRef.CreateConstReal(
                _typeBuilder.BuildType(node.DataType),
                double.Parse(node.Value.Value)
            ),
        };
    }

    private LLVMValueRef Visit(SemanticVariableReferenceNode node)
    {
        var (type, pointer) = ResolveVariableReference(node);

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private (LLVMTypeRef type, LLVMValueRef pointer) ResolveVariableReference(SemanticVariableReferenceNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var pointer = _moduleCache.GetNodeLlvmValue((SemanticNode)node.Symbol.SemanticDeclaration);

        return (type, pointer);
    }

    private LLVMValueRef Visit(SemanticFunctionReferenceNode node)
    {
        Debug.Assert(node.Symbol.SemanticDeclaration != null);

        var functionIdentifier = _contextCache.GetSymbolName(node.Symbol.SemanticDeclaration);
        var functionDeclaration = _module.GetNamedFunction(functionIdentifier);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
        {
            var functionType = BuildFunction(node.Symbol);

            functionDeclaration = _module.AddFunction(functionIdentifier, functionType);
        }

        var functionPointerType = _typeBuilder.BuildType(node.DataType);

        return _builder.BuildBitCast(functionDeclaration, functionPointerType, "functionPointer");
    }

    private LLVMTypeRef BuildFunction(FunctionSymbol symbol)
    {
        var parameterTypes = symbol.SemanticDeclaration!.Parameters
            .Select(x => x.DataType)
            .Select(_typeBuilder.BuildType)
            .ToList();
        var returnType = _typeBuilder.BuildType(symbol.SemanticDeclaration.ReturnType);

        if (!symbol.SyntaxDeclaration.IsStatic)
        {
            var parentSymbol = ((ISyntaxStructureDeclaration)symbol.SyntaxDeclaration.Parent!).Symbol!;
            var parentType = LLVMTypeRef.CreatePointer(_context.VoidType, 0);
            parameterTypes.Insert(0, parentType);
        }

        return LLVMTypeRef.CreateFunction(returnType, parameterTypes.ToArray());
    }

    private LLVMValueRef Visit(SemanticFieldReferenceNode node)
    {
        var (type, pointer) = ResolveFieldReference(node);

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private (LLVMTypeRef type, LLVMValueRef pointer) ResolveFieldReference(SemanticFieldReferenceNode node)
    {
        Debug.Assert(node.Symbol.SemanticDeclaration != null);

        var type = _typeBuilder.BuildType(node.DataType);
        var declaration = node.Symbol.SemanticDeclaration;

        // Static fields are generated as globals
        if (node.ObjectInstance == null)
            return (type, _moduleCache.GetNodeLlvmValue(declaration));

        var structure = _semanticTree.GetEnclosingStructure(node)!;
        var instanceDataType = (StructureDataType)node.ObjectInstance.DataType;
        var instanceLlvmType = _typeBuilder.BuildNamedStructType(instanceDataType);
        var instance = node.ObjectInstance == null
            ? GetSelf()
            : Next(node.ObjectInstance);

        // The first field is the type table
        uint fieldOffset = 1;

        var index = (uint)structure.FieldStartIndex + (uint)structure.Fields.IndexOf(declaration);
        var pointer = _builder.BuildStructGEP2(
            instanceLlvmType,
            instance!.Value,
            index + fieldOffset,
            $"{node.Identifier.Value}_pointer"
        );

        return (type, pointer);
    }

    private LLVMValueRef Visit(SemanticEnumReferenceNode node)
    {
        var valueNode = node
            .Symbol!
            .SemanticDeclaration!
            .Members
            .First(x => x.Identifier.Value == node.Identifier.Value)
            .Value;

        return Next(valueNode)!.Value;
    }

    private LLVMValueRef Visit(SemanticUnaryNode node)
    {
        var value = Next(node.Value);
        Debug.Assert(value.HasValue);

        if (node.Operator == TokenKind.Exclamation)
        {
            return _builder.BuildXor(value!.Value, LlvmUtils.CreateConstBool(_context, false), "not");
        }

        if (node.Operator == TokenKind.Minus)
        {
            return node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildNeg(value.Value, "neg"),
                var n when n.IsFloat() => _builder.BuildFNeg(value.Value, "neg"),
                _ => throw new InvalidOperationException(),
            };
        }

        throw new NotImplementedException();
    }

    private LLVMValueRef Visit(SemanticBinaryNode node)
    {
        var left = Next(node.Left);
        Debug.Assert(left.HasValue);

        if (node.Operator == TokenKind.AmpersandAmpersand)
            return _specialValueBuilder.BuildLogicalAnd(left.Value, () => Next(node.Right)!.Value);

        if (node.Operator == TokenKind.PipePipe)
            return _specialValueBuilder.BuildLogicalOr(left.Value, () => Next(node.Right)!.Value);

        var right = Next(node.Right);
        Debug.Assert(right.HasValue);

        if (node.Operator is
            TokenKind.EqualsEquals or
            TokenKind.NotEquals or
            TokenKind.Greater or
            TokenKind.GreaterEquals or
            TokenKind.Less or
            TokenKind.LessEquals
        )
        {
            if (node.DataType.IsInteger())
            {
                var isSigned = node.DataType.IsSignedInteger();
                var predicate = node.Operator switch
                {
                    TokenKind.EqualsEquals => LLVMIntPredicate.LLVMIntEQ,
                    TokenKind.NotEquals => LLVMIntPredicate.LLVMIntNE,
                    TokenKind.Greater => isSigned
                        ? LLVMIntPredicate.LLVMIntSGT
                        : LLVMIntPredicate.LLVMIntUGT,
                    TokenKind.GreaterEquals => isSigned
                        ? LLVMIntPredicate.LLVMIntSGE
                        : LLVMIntPredicate.LLVMIntUGE,
                    TokenKind.Less => isSigned
                        ? LLVMIntPredicate.LLVMIntSLT
                        : LLVMIntPredicate.LLVMIntULT,
                    TokenKind.LessEquals => isSigned
                        ? LLVMIntPredicate.LLVMIntSLT
                        : LLVMIntPredicate.LLVMIntULT,
                    _ => throw new UnreachableException(),
                };

                return _builder.BuildICmp(predicate, left.Value, right.Value, "cmp");
            }
            else if (node.DataType.IsFloat())
            {
                var predicate = node.Operator switch
                {
                    TokenKind.EqualsEquals => LLVMRealPredicate.LLVMRealOEQ,
                    TokenKind.NotEquals => LLVMRealPredicate.LLVMRealONE,
                    TokenKind.Greater => LLVMRealPredicate.LLVMRealOGT,
                    TokenKind.GreaterEquals => LLVMRealPredicate.LLVMRealOGE,
                    TokenKind.Less => LLVMRealPredicate.LLVMRealOLT,
                    TokenKind.LessEquals => LLVMRealPredicate.LLVMRealOLE,
                    _ => throw new UnreachableException(),
                };

                return _builder.BuildFCmp(predicate, left.Value, right.Value, "cmpf");
            }
        }

        return node.Operator switch
        {
            TokenKind.Plus => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildAdd(left.Value, right.Value, "add"),
                var n when n.IsFloat() => _builder.BuildFAdd(left.Value, right.Value, "addf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Minus => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildSub(left.Value, right.Value, "sub"),
                var n when n.IsFloat() => _builder.BuildFSub(left.Value, right.Value, "subf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Star => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildMul(left.Value, right.Value, "mul"),
                var n when n.IsFloat() => _builder.BuildFMul(left.Value, right.Value, "mulf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Slash => node.DataType switch
            {
                var n when n.IsSignedInteger() => _builder.BuildSDiv(left.Value, right.Value, "div"),
                var n when n.IsUnsignedInteger() => _builder.BuildUDiv(left.Value, right.Value, "div"),
                var n when n.IsFloat() => _builder.BuildFDiv(left.Value, right.Value, "divf"),
                _ => throw new InvalidOperationException(),
            },
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef Visit(SemanticAssignmentNode node)
    {
        LLVMValueRef pointer;
        if (node.Left is SemanticVariableReferenceNode variableReferenceNode)
        {
            pointer = ResolveVariableReference(variableReferenceNode).pointer;
        }
        else if (node.Left is SemanticFieldReferenceNode fieldReferenceNode)
        {
            pointer = ResolveFieldReference(fieldReferenceNode).pointer;
        }
        else
        {
            throw new NotImplementedException();
        }

        var value = Next(node.Right)!.Value;
        _builder.BuildStore(value!, pointer);

        return value;
    }

    private LLVMValueRef Visit(SemanticCallNode node)
    {
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToList();

        if (node.Left is SemanticFunctionReferenceNode { ObjectInstance: not null } functionReference)
        {
            var objectInstance = Next(functionReference.ObjectInstance)!.Value;
            if (functionReference.Symbol.IsVirtual)
                return BuildVirtualCall(node, objectInstance, functionReference, arguments);

            arguments.Insert(0, objectInstance);
        }

        var functionPointer = Next(node.Left)!.Value;
        var functionType = _typeBuilder.BuildFunctionType(((FunctionDataType)node.Left.DataType).Symbol);
        var function = _builder.BuildBitCast(functionPointer, functionType, "deref");
        var name = node.DataType.IsVoid()
            ? string.Empty
            : "call";

        return _builder.BuildCall2(functionType, functionPointer, arguments.ToArray(), name);
    }

    private LLVMValueRef BuildVirtualCall(
        SemanticCallNode callNode,
        LLVMValueRef objectInstance,
        SemanticFunctionReferenceNode functionReference,
        List<LLVMValueRef> arguments
    )
    {
        var instanceDataType = (StructureDataType)functionReference.ObjectInstance!.DataType;
        var vtableType =_typeBuilder.BuildVtableType(instanceDataType.Symbol);
        var vtablePointerType = LLVMTypeRef.CreatePointer(vtableType, 0);

        int functionIndex;
        LLVMValueRef vtablePointer;
        if (instanceDataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            arguments.Insert(0, objectInstance);

            // Get the type table
            var instanceType = _typeBuilder.BuildStructType(instanceDataType);
            var typeTableType = _typeBuilder.BuildTypeTableType(instanceDataType.Symbol);
            var typeTablePointerType = LLVMTypeRef.CreatePointer(typeTableType, 0);
            var typeTablePointerPointer = _builder.BuildStructGEP2(
                instanceType,
                objectInstance,
                0,
                "typeTablePointerPointer"
            );
            var typeTablePointer = _builder.BuildLoad2(typeTablePointerType, typeTablePointerPointer, "typeTablePointer");

            // Get the vtable
            var vtablePointerPointer = _builder.BuildStructGEP2(
                typeTableType,
                typeTablePointer,
                (uint)TypeTableField.Vtable,
                "vtablePointerPointer"
            );
            vtablePointer = _builder.BuildLoad2(vtablePointerType, vtablePointerPointer, "vtable");

            functionIndex = classNode
                .GetAllMethods()
                .Index()
                .First(x => x.Item.Symbol == functionReference.Symbol)
                .Index;
        }
        else
        {
            // Protocol
            var fatPointerType = _typeBuilder.BuildType(instanceDataType);
            var instancePointer = _builder.BuildAlloca(fatPointerType, "instancePointer");
            _builder.BuildStore(objectInstance, instancePointer);

            // Extract the concrete instance from the fat pointer
            var concreteInstance = _builder.BuildStructGEP2(
                fatPointerType,
                instancePointer,
                (uint)FatPointerField.Instance,
                "lean"
            );
            arguments.Insert(0, concreteInstance);

            // Get the vtable
            var vtablePointerPointer = _builder.BuildStructGEP2(
                fatPointerType,
                instancePointer,
                (uint)FatPointerField.Vtable,
                "vtablePointerPointer"
            );
            vtablePointer = _builder.BuildLoad2(vtablePointerType, vtablePointerPointer, "vtable");

            functionIndex = instanceDataType
                .Symbol
                .SemanticDeclaration!
                .Functions
                .IndexOf(functionReference.Symbol.SemanticDeclaration!);
        }


        // Get the function from the vtable
        var functionDeclaration = _typeBuilder.BuildFunctionType(((FunctionDataType)callNode.Left.DataType).Symbol);
        var functionPointerPointer = _builder.BuildStructGEP2(vtableType, vtablePointer, (uint)functionIndex, "functionPointerPointer");
        var functionPointer = _builder.BuildLoad2(
            LLVMTypeRef.CreatePointer(functionDeclaration, 0),
            functionPointerPointer,
            "functionPointer"
        );

        var name = callNode.DataType.IsVoid()
            ? string.Empty
            : "call";

        return _builder.BuildCall2(functionDeclaration, functionPointer, arguments.ToArray(), name);
    }

    private LLVMValueRef Visit(SemanticNewNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var structure = (ISemanticInstantiableStructureDeclaration)((StructureDataType)node.DataType).Symbol.SemanticDeclaration!;
        var instance = _specialValueBuilder.BuildMalloc(type, "new");
        var arguments = node
            .Arguments
            .Select(Next)
            .Select(x => x!.Value);
        BuildConstructorCall(instance, structure, arguments);

        return instance;
    }

    private void BuildConstructorCall(
        LLVMValueRef instance,
        ISemanticInstantiableStructureDeclaration structure,
        IEnumerable<LLVMValueRef> arguments
    )
    {
        var builtArguments = arguments
            .Prepend(instance)
            .ToArray();

        var initIdentifier = _contextCache.GetSymbolName(structure.Init);
        var functionDeclaration = _module.GetNamedFunction(initIdentifier);
        var functionType = _typeBuilder.BuildInitType(structure.Init);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
            functionDeclaration = _module.AddFunction(initIdentifier, functionType);

        _builder.BuildCall2(functionType, functionDeclaration, builtArguments);
    }

    private LLVMValueRef Visit(SemanticReturnNode node)
    {
        var returnTypeNode = _semanticTree.GetEnclosingFunction(node)!.ReturnType;
        var returnType = _typeBuilder.BuildType(returnTypeNode);

        if (node.Value == null)
        {
            _builder.BuildRetVoid();

            return LLVMValueRef.CreateConstNull(returnType);
        }

        var value = Next(node.Value);
        _builder.BuildRet(value!.Value);

        return value.Value;
    }

    private LLVMValueRef Visit(SemanticKeywordValueNode node)
    {
        if (node.Keyword.Value == "size_of")
        {
            var type =_typeBuilder.BuildType(node.Arguments![0].DataType);

            return LlvmUtils.BuildSizeOf(_context, _module, type);
        }

        if (node.Keyword.Kind is TokenKind.Self or TokenKind.Base)
            return GetSelf();

        throw new UnreachableException();
    }

    private LLVMValueRef Visit(SemanticCastNode node)
    {
        var fromType = node.Value.DataType;
        var toType = node.DataType;

        var value = Next(node.Value)!.Value;
        if (toType is EnumDataType toEnum)
            toType = toEnum.Symbol.SemanticDeclaration!.MemberDataType;

        if (fromType is EnumDataType fromEnum)
            fromType = fromEnum.Symbol.SemanticDeclaration!.MemberDataType;

        if (toType is PrimitiveDataType primitiveDataType)
            return BuildPrimitiveCast(value, fromType, primitiveDataType);

        if (toType.IsClass() && fromType.IsProtocol())
        {
            // Extract the instance pointer from the fat pointer
            var fatPointerType = _typeBuilder.BuildType((StructureDataType)fromType);

            return _builder.BuildStructGEP2(
                fatPointerType,
                value,
                (uint)FatPointerField.Instance,
                "lean"
            );
        }

        if (toType.IsProtocol() && fromType.IsClass())
        {
            // TODO: Runtime check to make sure it's the correct type
            return BuildFatPointer(value, (StructureDataType)fromType, (StructureDataType)toType);
        }

        if (toType.IsProtocol() && fromType.IsProtocol())
        {
            // TODO: Runtime check to make sure it's the correct type

            // Extract the instance pointer from the existing fat pointer
            var fatPointerType = _typeBuilder.BuildType((StructureDataType)fromType);
            var fromTypeInstance = _builder.BuildStructGEP2(
                fatPointerType,
                value,
                (uint)FatPointerField.Instance,
                "lean"
            );

            return BuildFatPointer(fromTypeInstance, (StructureDataType)fromType, (StructureDataType)toType);
        }

        if (toType.IsClass() && fromType.IsClass())
        {
            // TODO: Runtime check to make sure it's the correct type
            return value;
        }

        throw new NotImplementedException();
    }

    private LLVMValueRef BuildFatPointer(LLVMValueRef instance, StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        var implementor = implementorDataType.Symbol.SemanticDeclaration!;
        var implemented = implementedDataType.Symbol.SemanticDeclaration!;

        var fatPointerType = _typeBuilder.BuildType(implementedDataType);
        var alloca = _builder.BuildAlloca(fatPointerType);
        var instancePointer = _builder.BuildStructGEP2(
            fatPointerType,
            alloca,
            (uint)FatPointerField.Instance,
            "instance"
        );
        var vtablePointer = _builder.BuildStructGEP2(
            fatPointerType,
            alloca,
            (uint)FatPointerField.Vtable,
            "vtable"
        );
        var vtableValue = BuildVtable(implementor, implemented);

        _builder.BuildStore(instance, instancePointer);
        _builder.BuildStore(vtableValue, vtablePointer);

        return _builder.BuildLoad2(fatPointerType, alloca, "fatPointer");
    }

    private LLVMValueRef BuildVtable(ISemanticStructureDeclaration implementor, ISemanticStructureDeclaration implemented)
    {
        var vtableName = _contextCache.GetVtableName(implementor, implemented);

        return _module.GetNamedGlobal(vtableName);
    }

    private LLVMValueRef BuildPrimitiveCast(
        LLVMValueRef value,
        IDataType fromDataType,
        PrimitiveDataType toPrimitive
    )
    {
        if (fromDataType is not PrimitiveDataType fromPrimitive)
            throw new NotImplementedException();

        bool isUpcast = toPrimitive.BitSize > fromPrimitive.BitSize;
        bool isDowncast = toPrimitive.BitSize < fromPrimitive.BitSize;
        var toLlvmType = _typeBuilder.BuildType(toPrimitive);

        LLVMOpcode? opCode;
        if (toPrimitive.IsInteger())
        {
            opCode = fromPrimitive switch
            {
                var from when from.IsSignedInteger() && isUpcast => LLVMOpcode.LLVMZExt,
                var from when from.IsUnsignedInteger() && isUpcast => LLVMOpcode.LLVMSExt,
                var from when from.IsInteger() && isDowncast => LLVMOpcode.LLVMTrunc,
                var from when from.IsFloat() && toPrimitive.IsSignedInteger() => LLVMOpcode.LLVMFPToSI,
                var from when from.IsFloat() && toPrimitive.IsUnsignedInteger() => LLVMOpcode.LLVMFPToUI,
                _ => null,
            };
        }
        else if (toPrimitive.IsFloat())
        {
            opCode = fromPrimitive switch
            {
                var from when from.IsSignedInteger() => LLVMOpcode.LLVMSIToFP,
                var from when from.IsUnsignedInteger() => LLVMOpcode.LLVMUIToFP,
                var from when from.IsFloat() && isUpcast => LLVMOpcode.LLVMFPExt,
                var from when from.IsFloat() && isDowncast => LLVMOpcode.LLVMFPTrunc,
                _ => null,
            };
        }
        else
        {
            throw new NotImplementedException();
        }

        if (!opCode.HasValue)
            return value;

        return _builder.BuildCast(opCode.Value, value, toLlvmType, "cast");
    }

    private LLVMValueRef? Visit(SemanticBlockNode node)
    {
        // If it's the root block, we shouldn't create an LLVM basic block.
        // Just emit the children.
        if (node.Parent == null)
        {
            foreach (var expression in node.Expressions)
                Next(expression);

            return null;
        }

        var function = _builder.InsertBlock.Parent;
        var value = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node, value);
        _builder.PositionAtEnd(value);

        LLVMValueRef? lastValue = null;
        foreach (var expression in node.Expressions)
            lastValue = Next(expression);

        return lastValue;
    }

    private LLVMValueRef Visit(SemanticVariableDeclarationNode node)
    {
        var block = _builder.InsertBlock;
        var firstInstruction = block.FirstInstruction;
        if (firstInstruction != null)
            _builder.PositionBefore(firstInstruction);

        var alloca = _builder.BuildAlloca(
            _typeBuilder.BuildType(node.DataType),
            node.Identifier.Value
        );

        _builder.PositionAtEnd(block);
        _moduleCache.SetNodeLlvmValue(node, alloca);

        var value = Next(node.Value);
        _builder.BuildStore(value!.Value, alloca);

        return value.Value;
    }

    private LLVMValueRef? Visit(SemanticFunctionDeclarationNode node)
    {
        var function = _moduleCache.GetNodeLlvmValue(node);
        if (node.Body == null)
            return function;

        var debugName = _contextCache.GetSymbolName(node);
        BuildFunctionDi(
            function,
            node.Span,
            debugName,
            isArtificial: false
        );

        var block = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node.Body, block);
        _builder.PositionAtEnd(block);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _typeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _builder.BuildAlloca(parameterType, $"{parameter.Identifier.Value}.addr");
            _moduleCache.SetNodeLlvmValue(parameter, parameterPointer);
        }

        // Store parameters
        var parameterOffset = node.IsStatic ? (uint)0 : 1;
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i + parameterOffset);
            var parameterPointer = _moduleCache.GetNodeLlvmValue(parameter);
            _builder.BuildStore(parameterValue, parameterPointer);
        }

        foreach (var expression in node.Body.Expressions.SkipLast(1))
            Next(expression);

        var last = node.Body.Expressions.LastOrDefault();
        if (last is SemanticReturnNode)
        {
            Next(last);
        }
        else if (last == null)
        {
            _builder.BuildRetVoid();
        }
        else if (node.ReturnType is PrimitiveDataType { Kind: Primitive.Void })
        {
            Next(last);
            _builder.BuildRetVoid();
        }
        else
        {
            var lastValue = Next(last);
            if (lastValue.HasValue)
                _builder.BuildRet(lastValue.Value);
        }

        return function;
    }

    private void BuildFunctionDi(LLVMValueRef llvmFunction, TextSpan span, string name, bool isArtificial)
    {
        // TODO: Add more type metadata and DI flags?
        var flags = LLVMDIFlags.LLVMDIFlagZero;
        if (isArtificial)
            flags |= LLVMDIFlags.LLVMDIFlagArtificial;

        var functionType = _diBuilder.CreateSubroutineType(_diFile, [], flags);
        var line = (uint)span.Start.Line + 1;
        var debugFunction = _diBuilder.CreateFunction(
            Scope: _compileUnit,
            Name: name,
            LinkageName: name,
            File: _diFile,
            LineNo: line,
            Type: functionType,
            IsLocalToUnit: 1,
            IsDefinition: 1,
            ScopeLine: line,
            Flags: flags,
            IsOptimized: 0
        );

        var currentLine = _context.CreateDebugLocation(line, 0, debugFunction, default);
        _currentFunctionMetadata = debugFunction;

        unsafe
        {
            LLVM.SetCurrentDebugLocation2(_builder, currentLine);
            LLVM.SetSubprogram(llvmFunction, debugFunction);
        }
    }

    private LLVMValueRef? Visit(SemanticClassDeclarationNode node)
    {
        Visit(node.Init, node);

        foreach (var staticField in node.Fields.Where(x => x.IsStatic))
            BuildStaticField(staticField);

        foreach (var function in node.Functions)
            Next(function);

        if (node.Identifier.Value == "Program")
            BuildMainFunction(node);

        return null;
    }

    private void BuildStaticField(SemanticFieldDeclarationNode field)
    {
        if (field.Attributes.Any(x => x.Identifier.Value == "ffi"))
            return;

        var global = _moduleCache.GetNodeLlvmValue(field);
        global.Initializer = field.Value == null
            ? _specialValueBuilder.BuildDefaultValueForType(field.DataType)
            : Next(field.Value)!.Value;
    }

    private LLVMValueRef Visit(SemanticInitNode node, SemanticClassDeclarationNode parentStructure)
    {
        var function = _moduleCache.GetNodeLlvmValue(node);

        var debugName = _contextCache.GetSymbolName(node);
        BuildFunctionDi(
            function,
            parentStructure.Span,
            debugName,
            isArtificial: true
        );

        var value = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node.Body, value);
        _builder.PositionAtEnd(value);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _typeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _builder.BuildAlloca(parameterType, $"{parameter.Identifier.Value}.addr");
            _moduleCache.SetNodeLlvmValue(parameter, parameterPointer);
        }

        // Store parameters
        uint parameterOffset = 1;
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i + parameterOffset);
            var parameterPointer = _moduleCache.GetNodeLlvmValue(parameter);
            _builder.BuildStore(parameterValue, parameterPointer);
        }

        // Insert default values
        var instance = GetSelf();
        var type = _typeBuilder.BuildNamedStructType(new StructureDataType(parentStructure.Symbol));


        uint fieldOffset = parentStructure is SemanticClassDeclarationNode
            ? (uint)1
            : (uint)0;
        foreach (var (i, field) in parentStructure.GetAllMemberFields().Index())
        {
            var fieldPointer = _builder.BuildStructGEP2(type, instance, (uint)i + fieldOffset, field.Identifier.Value);
            var fieldValue = field.Value == null
                ? _specialValueBuilder.BuildDefaultValueForType(field.DataType)
                : Next(field.Value);
            _builder.BuildStore(fieldValue!.Value, fieldPointer);
        }

        // Call base constructor (if any)
        if (parentStructure.InheritedClass != null)
        {
            IEnumerable<LLVMValueRef> baseCallArguments = [];
            if (node.BaseCall != null)
            {
                baseCallArguments = node
                    .BaseCall
                    .Arguments!
                    .Select(Next)
                    .Select(x => x!.Value);
            }

            BuildConstructorCall(
                instance,
                (SemanticClassDeclarationNode)parentStructure.InheritedClass.SemanticDeclaration!,
                baseCallArguments
            );
        }

        if (parentStructure is SemanticClassDeclarationNode parentClass)
        {
            // Insert type table
            var typeTable = BuildTypeTable(parentClass);
            var typeTableType = _typeBuilder.BuildTypeTableType(parentStructure.Symbol);
            var typeTablePointer = _builder.BuildStructGEP2(type, instance, 0, "typeTable");
            _builder.BuildStore(typeTable, typeTablePointer);

            // The first field is the type table
            fieldOffset = 1;
        }

        foreach (var expression in node.Body.Expressions)
            Next(expression);

        _builder.BuildRetVoid();

        return function;
    }

    private LLVMValueRef BuildTypeTable(SemanticClassDeclarationNode classNode)
    {
        var typeTableName = _contextCache.GetTypeTableName(classNode);

        return _module.GetNamedGlobal(typeTableName);
    }

    private LLVMValueRef? Visit(SemanticModuleDeclarationNode node)
    {
        foreach (var staticField in node.Fields.Where(x => x.IsStatic))
            BuildStaticField(staticField);

        foreach (var function in node.Functions)
            Next(function);

        if (node.Identifier.Value == "Program")
            BuildMainFunction(node);

        return null;
    }

    private void BuildMainFunction(ISemanticStructureDeclaration structure)
    {
        // Create the entry function
        var entryFunctionType = LLVMTypeRef.CreateFunction(_context.Int32Type, []);
        var entryFunction = _module.AddFunction("main", entryFunctionType);
        var block = entryFunction.AppendBasicBlock("entry");
        _builder.PositionAtEnd(block);

        // Find the user-defined main function
        var userEntryDeclaration = structure.Functions.FirstOrDefault(x => x.Identifier.Value == "Main");
        if (userEntryDeclaration == null)
            return;

        // Build a call to the user-defined main function
        var userMainFunction = _moduleCache.GetNodeLlvmValue(userEntryDeclaration);
        var userMainFunctionType = _typeBuilder.BuildFunctionType(userEntryDeclaration.Symbol);
        var returnsVoid = userEntryDeclaration.ReturnType is PrimitiveDataType { Kind: Primitive.Void };
        var returnValue = _builder.BuildCall2(
            userMainFunctionType,
            userMainFunction,
            Array.Empty<LLVMValueRef>(),
            returnsVoid ? string.Empty : "main"
        );

        if (returnsVoid)
        {
            _builder.BuildRet(LLVMValueRef.CreateConstInt(_context.Int32Type, 0, false));
        }
        else
        {
            _builder.BuildRet(returnValue);
        }
    }
}
