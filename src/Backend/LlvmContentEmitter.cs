using System.Diagnostics;
using System.Runtime.InteropServices;
using Caique.Analysis;
using Caique.Lexing;
using Caique.Lowering;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public record EmitResult(string ObjectFilePath, bool Success);

public class LlvmContentEmitter
{
    private readonly LoweredTree _tree;
    private readonly LLVMContextRef _context;
    private readonly LLVMBuilderRef _builder;
    private readonly LLVMModuleRef _module;
    private readonly LlvmTypeBuilder _typeBuilder;
    private readonly LlvmSpecialValueBuilder _specialValueBuilder;
    private readonly LLVMDIBuilderRef _diBuilder;
    private readonly LLVMMetadataRef _compileUnit;
    private readonly LLVMMetadataRef _diFile;
    private readonly Dictionary<string, LLVMValueRef> _functions = [];
    private readonly Dictionary<string, LLVMValueRef> _globals = [];
    private readonly Dictionary<LoweredNode, LLVMValueRef> _variables = [];
    private LLVMMetadataRef? _currentFunctionMetadata;

    private LlvmContentEmitter(LoweredTree tree, LlvmEmitterContext emitterContext)
    {
        _tree = tree;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _specialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _typeBuilder);
        var (diBuilder, compileUnit, diFile) = SetUpDiBuilder(_context, _module, tree.FilePath);
        _diBuilder = diBuilder;
        _compileUnit = compileUnit;
        _diFile = diFile;
    }

    public static EmitResult Emit(
        LoweredTree tree,
        LlvmEmitterContext emitterContext,
        string targetDirectory,
        CompilationOptions? compilationOptions = null
    )
    {
        var emitter = new LlvmContentEmitter(tree, emitterContext);
        emitter.EmitHeaders(tree);

        foreach (var function in tree.Functions.Values)
            emitter.Next(function);

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

        return new EmitResult(objectPath, isValid);
    }

    private static (LLVMDIBuilderRef, LLVMMetadataRef, LLVMMetadataRef) SetUpDiBuilder(
        LLVMContextRef context,
        LLVMModuleRef module,
        string sourcePath
    )
    {
        var metadata = LLVMValueRef.CreateConstInt(context.Int32Type, 3);
        var key = context.GetMDString("Debug Info Version");
        var kind = LLVMValueRef.CreateConstInt(context.Int32Type, 2);
        var value = context.GetMDNode([kind, key, metadata]);
        module.AddNamedMetadataOperand("llvm.module.flags", value);

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

    public void EmitHeaders(LoweredTree tree)
    {
        foreach (var declaration in tree.Functions.Values)
            AddFunction(declaration.Identifier, declaration.DataType);

        foreach (var declaration in tree.Globals.Values)
            AddGlobal(declaration);
    }

    private LLVMValueRef AddFunction(string identifier, ILoweredDataType dataType)
    {
        var functionType = _typeBuilder.BuildType(dataType);
        var value = _module.AddFunction(identifier, functionType);
        _functions[identifier] = value;

        return value;
    }

    private void AddGlobal(LoweredGlobalDeclarationNode node)
    {
        if (node.Value is LoweredLiteralNode literalNode && literalNode.Kind == TokenKind.StringLiteral)
        {
            var stringType = LLVMTypeRef.CreateArray(_context.Int8Type, (uint)literalNode.Value.Length);
            var stringGlobal = _module.AddGlobal(stringType, node.Identifier);
            var stringConstant = _context.GetConstString(literalNode.Value, DontNullTerminate: true);
            stringGlobal.Initializer = stringConstant;
            stringGlobal.IsGlobalConstant = node.Scope == LoweredGlobalScope.Full;

            _globals[node.Identifier] = stringGlobal;

            return;
        }

        var dataType = _typeBuilder.BuildType(node.DataType);
        var global = _module.AddGlobal(dataType, node.Identifier);
        if (node.Value != null)
            global.Initializer = Next(node.Value)!.Value;

        global.IsGlobalConstant = node.Scope == LoweredGlobalScope.Full;

        _globals[node.Identifier] = global;
    }

    private LLVMValueRef? Next(LoweredNode node)
    {
        return node switch
        {
            LoweredStatementStartNode statementNode => Visit(statementNode),
            LoweredLoadNode loadNode => Visit(loadNode),
            LoweredLiteralNode literalNode => Visit(literalNode),
            LoweredVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            LoweredFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            LoweredFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            LoweredGlobalReferenceNode globalReferenceNode => Visit(globalReferenceNode),
            LoweredUnaryNode unaryNode => Visit(unaryNode),
            LoweredBinaryNode binaryNode => Visit(binaryNode),
            LoweredAssignmentNode assignmentNode => Visit(assignmentNode),
            LoweredCallNode callNode => Visit(callNode),
            LoweredReturnNode returnNode => Visit(returnNode),
            LoweredKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            LoweredIfNode ifNode => Visit(ifNode),
            LoweredCastNode castNode => Visit(castNode),
            LoweredBlockNode blockNode => Visit(blockNode),
            LoweredVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            LoweredFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            LoweredConstStructNode constStructNode => Visit(constStructNode),
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef GetSelf()
    {
        return _builder.InsertBlock.Parent.GetParam(0);
    }

    private LLVMValueRef? Visit(LoweredStatementStartNode node)
    {
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

        return null;
    }

    private LLVMValueRef Visit(LoweredLoadNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var value = Next(node.Value)!.Value;

        return _builder.BuildLoad2(type, value, "load");
    }

    private LLVMValueRef Visit(LoweredLiteralNode node)
    {
        var primitive = (LoweredPrimitiveDataType)node.DataType;

        return primitive.Primitive switch
        {
            Primitive.Void => throw new InvalidOperationException(),
            Primitive.Null => LLVMValueRef.CreateConstNull(LLVMTypeRef.Void),
            Primitive.Bool => LlvmUtils.CreateConstBool(_context, node.Kind == TokenKind.True),
            >= Primitive.Int8 and <= Primitive.Int128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value),
                SignExtend: true
            ),
            >= Primitive.Uint8 and <= Primitive.Uint128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value),
                SignExtend: false
            ),
            >= Primitive.Float16 and <= Primitive.Float64 => LLVMValueRef.CreateConstReal(
                _typeBuilder.BuildType(node.DataType),
                double.Parse(node.Value)
            ),
        };
    }

    private LLVMValueRef Visit(LoweredVariableReferenceNode node)
    {
        return _variables[(LoweredNode)node.Declaration];
    }

    private LLVMValueRef Visit(LoweredFunctionReferenceNode node)
    {
        var functionDeclaration = _module.GetNamedFunction(node.Identifier);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
            functionDeclaration = AddFunction(node.Identifier, node.DataType.Dereference());

        var functionPointerType = _typeBuilder.BuildType(node.DataType);

        return _builder.BuildBitCast(functionDeclaration, functionPointerType, "functionPointer");
    }

    private LLVMValueRef Visit(LoweredFieldReferenceNode node)
    {
        var instance = Next(node.Instance)!.Value;
        var loweredInstanceType = node.Instance.DataType.Dereference();
        var instanceType = _typeBuilder.BuildType(loweredInstanceType);

        return _builder.BuildStructGEP2(
            instanceType,
            instance,
            (uint)node.Index,
            $"field_{node.Index}_pointer"
        );
    }

    private LLVMValueRef Visit(LoweredGlobalReferenceNode node)
    {
        var global = _module.GetNamedGlobal(node.Identifier);
        if (string.IsNullOrEmpty(global.Name))
        {
            var dataType = _typeBuilder.BuildType(node.DataType);
            global = _module.AddGlobal(dataType, node.Identifier);
        }

        return global;
    }

    private LLVMValueRef Visit(LoweredUnaryNode node)
    {
        var value = Next(node.Value);
        Debug.Assert(value.HasValue);

        if (node.Operator == TokenKind.Exclamation)
            return _builder.BuildXor(value!.Value, LlvmUtils.CreateConstBool(_context, true), "not");

        if (node.Operator == TokenKind.Minus)
        {
            return node.DataType switch
            {
                LoweredPrimitiveDataType n when n.Primitive.IsInteger() => _builder.BuildNeg(value.Value, "neg"),
                LoweredPrimitiveDataType n when n.Primitive.IsFloat() => _builder.BuildFNeg(value.Value, "neg"),
                _ => throw new InvalidOperationException(),
            };
        }

        throw new NotImplementedException();
    }

    private LLVMValueRef Visit(LoweredBinaryNode node)
    {
        var left = Next(node.Left);
        Debug.Assert(left.HasValue);

        if (node.Operator == TokenKind.AmpersandAmpersand)
            return _specialValueBuilder.BuildLogicalAnd(left.Value, () => Next(node.Right)!.Value);

        if (node.Operator == TokenKind.PipePipe)
            return _specialValueBuilder.BuildLogicalOr(left.Value, () => Next(node.Right)!.Value);

        var right = Next(node.Right);
        Debug.Assert(right.HasValue);

        var primitive = (node.Left.DataType as LoweredPrimitiveDataType)?.Primitive;
        if (node.Operator is
            TokenKind.EqualsEquals or
            TokenKind.NotEquals or
            TokenKind.Greater or
            TokenKind.GreaterEquals or
            TokenKind.Less or
            TokenKind.LessEquals
        )
        {
            if (primitive?.IsInteger() is true)
            {
                var isSigned = primitive?.IsSignedInteger() is true;
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
            else if (primitive?.IsFloat() is true)
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
            TokenKind.Plus => primitive switch
            {
                var n when n?.IsInteger() is true => _builder.BuildAdd(left.Value, right.Value, "add"),
                var n when n?.IsFloat() is true => _builder.BuildFAdd(left.Value, right.Value, "addf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Minus => primitive switch
            {
                var n when n?.IsInteger() is true => _builder.BuildSub(left.Value, right.Value, "sub"),
                var n when n?.IsFloat() is true => _builder.BuildFSub(left.Value, right.Value, "subf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Star => primitive switch
            {
                var n when n?.IsInteger() is true => _builder.BuildMul(left.Value, right.Value, "mul"),
                var n when n?.IsFloat() is true => _builder.BuildFMul(left.Value, right.Value, "mulf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Slash => primitive switch
            {
                var n when n?.IsSignedInteger() is true => _builder.BuildSDiv(left.Value, right.Value, "div"),
                var n when n?.IsUnsignedInteger() is true => _builder.BuildUDiv(left.Value, right.Value, "div"),
                var n when n?.IsFloat() is true => _builder.BuildFDiv(left.Value, right.Value, "divf"),
                _ => throw new InvalidOperationException(),
            },
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef Visit(LoweredAssignmentNode node)
    {
        var pointer = Next(node.Assignee)!.Value;
        var value = Next(node.Value)!.Value;
        _builder.BuildStore(value!, pointer);

        return value;
    }

    private LLVMValueRef Visit(LoweredCallNode node)
    {
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToList();

        var functionPointer = Next(node.Callee)!.Value;
        var functionType = _typeBuilder.BuildType(node.Callee.DataType.Dereference());
        var function = _builder.BuildBitCast(
            functionPointer,
            LLVMTypeRef.CreatePointer(functionType, 0),
            "deref"
        );
        var name = node.DataType is LoweredPrimitiveDataType { Primitive: Primitive.Void }
            ? string.Empty
            : "call";

        return _builder.BuildCall2(functionType, function, arguments.ToArray(), name);
    }

    private LLVMValueRef Visit(LoweredReturnNode node)
    {
        if (node.Value == null)
        {
            _builder.BuildRetVoid();

            return null;
        }

        var value = Next(node.Value);
        _builder.BuildRet(value!.Value);

        return value.Value;
    }

    private LLVMValueRef Visit(LoweredKeywordValueNode node)
    {
        if (node.Kind == KeywordValueKind.SizeOf)
        {
            var type = _typeBuilder.BuildType(node.Arguments![0].DataType);

            return LlvmUtils.BuildSizeOf(_context, _module, type);
        }

        if (node.Kind is KeywordValueKind.Self or KeywordValueKind.Base)
            return GetSelf();

        if (node.Kind is KeywordValueKind.Default)
            return _specialValueBuilder.BuildDefaultValueForType(node.DataType);

        throw new UnreachableException();
    }

    private LLVMValueRef? Visit(LoweredIfNode node)
    {
        var thenBlockReturns = node.ThenBranch.Expressions.LastOrDefault() is LoweredReturnNode;
        var elseBlockReturns = node.ElseBranch?.Expressions.LastOrDefault() is LoweredReturnNode;

        var parent = _builder.InsertBlock.Parent;
        var thenBlock = parent.AppendBasicBlock("if.then");
        var elseBlock = parent.AppendBasicBlock("if.else");
        var mergeBlock = thenBlockReturns && elseBlockReturns
            ? null
            : parent.AppendBasicBlock("if.end");

        var condition = Next(node.Condition)!.Value;
        _builder.BuildCondBr(condition, thenBlock, elseBlock);

        _builder.PositionAtEnd(thenBlock);
        var thenValue = Visit(node.ThenBranch);
        if (!thenBlockReturns)
            _builder.BuildBr(mergeBlock);

        _builder.PositionAtEnd(elseBlock);
        LLVMValueRef elseValue = LLVMValueRef.CreateConstNull(_context.Int8Type);
        if (node.ElseBranch != null)
            elseValue = Visit(node.ElseBranch);

        if (!elseBlockReturns)
            _builder.BuildBr(mergeBlock);

        if (mergeBlock != null)
            _builder.PositionAtEnd(mergeBlock);

        if (node.ThenBranch.ReturnValueDeclaration != null)
        {
            var type = _typeBuilder.BuildType(node.DataType);
            var phi = _builder.BuildPhi(type, "if.phi");
            phi.AddIncoming([thenValue], [thenBlock], 1);
            phi.AddIncoming([elseValue], [elseBlock], 1);

            return phi;
        }

        return null;
    }

    private LLVMValueRef Visit(LoweredCastNode node)
    {
        var fromType = node.Value.DataType;
        var toType = node.DataType;

        var value = Next(node.Value)!.Value;
        if (toType is LoweredPrimitiveDataType primitiveType)
            return BuildPrimitiveCast(value, fromType, primitiveType);

        if (toType is LoweredPointerDataType pointerType)
            return BuildPointerCast(value, pointerType);

        throw new NotImplementedException();
    }

    private LLVMValueRef BuildPrimitiveCast(
        LLVMValueRef value,
        ILoweredDataType fromDataType,
        LoweredPrimitiveDataType toPrimitive
    )
    {
        if (fromDataType is not LoweredPrimitiveDataType fromPrimitiveDataType)
            throw new NotImplementedException();

        var fromPrimitive = fromPrimitiveDataType.Primitive;
        bool isUpcast = toPrimitive.Primitive.GetBitSize() > fromPrimitive.GetBitSize();
        bool isDowncast = toPrimitive.Primitive.GetBitSize() < fromPrimitive.GetBitSize();
        var toLlvmType = _typeBuilder.BuildType(toPrimitive);

        LLVMOpcode? opCode;
        if (toPrimitive.Primitive.IsInteger())
        {
            opCode = fromPrimitive switch
            {
                var from when from.IsSignedInteger() && isUpcast => LLVMOpcode.LLVMZExt,
                var from when from.IsUnsignedInteger() && isUpcast => LLVMOpcode.LLVMSExt,
                var from when from.IsInteger() && isDowncast => LLVMOpcode.LLVMTrunc,
                var from when from.IsFloat() && toPrimitive.Primitive.IsSignedInteger() => LLVMOpcode.LLVMFPToSI,
                var from when from.IsFloat() && toPrimitive.Primitive.IsUnsignedInteger() => LLVMOpcode.LLVMFPToUI,
                _ => null,
            };
        }
        else if (toPrimitive.Primitive.IsFloat())
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

    private LLVMValueRef BuildPointerCast(LLVMValueRef value, LoweredPointerDataType toType)
    {
        var type = _typeBuilder.BuildType(toType);

        return _builder.BuildIntToPtr(value, type, "intToPtr");
    }

    private LLVMValueRef Visit(LoweredBlockNode node)
    {
        foreach (var expression in node.Expressions)
        {
            Next(expression);

            if (expression is LoweredReturnNode)
                break;
        }

        if (node.ReturnValueDeclaration != null)
        {
            var type = _typeBuilder.BuildType(node.DataType);
            var returnDeclaration = _variables[node.ReturnValueDeclaration];

            return _builder.BuildLoad2(type, returnDeclaration, "returnValue");
        }

        return LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(_context.Int8Type, 0));
    }

    private LLVMValueRef? Visit(LoweredVariableDeclarationNode node)
    {
        var block = _builder.InsertBlock;
        var firstInstruction = block.FirstInstruction;
        if (firstInstruction != null)
            _builder.PositionBefore(firstInstruction);

        var alloca = _builder.BuildAlloca(
            _typeBuilder.BuildType(node.DataType),
            node.Identifier
        );

        _builder.PositionAtEnd(block);
        _variables[node] = alloca;

        if (node.Value != null)
        {
            var value = Next(node.Value);
            _builder.BuildStore(value!.Value, alloca);

            return alloca;
        }

        return alloca;
    }

    private LLVMValueRef? Visit(LoweredFunctionDeclarationNode node)
    {
        var function = _functions[node.Identifier];
        if (node.Body == null)
            return function;

        BuildFunctionDi(
            function,
            node.Span,
            node.Identifier,
            isArtificial: false
        );

        var block = function.AppendBasicBlock("entry");
        _builder.PositionAtEnd(block);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _typeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _builder.BuildAlloca(parameterType, $"{parameter.Identifier}.addr");
            _variables[parameter] = parameterPointer;
        }

        // Store parameters
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i);
            var parameterPointer = _variables[parameter];
            _builder.BuildStore(parameterValue, parameterPointer);
        }

        foreach (var expression in node.Body.Expressions)
            Next(expression);

        if (node.Identifier.EndsWith("Main:Run"))
        {
            var functionType = _typeBuilder.BuildType(node.DataType);
            BuildMain(functionType, function, node.ReturnType);
        }

        return function;
    }

    private void BuildMain(LLVMTypeRef runFunctionType, LLVMValueRef runFunction, ILoweredDataType returnType)
    {
        var llvmReturnType = _context.Int32Type;
        var parameterTypes = Array.Empty<LLVMTypeRef>();
        var type = LLVMTypeRef.CreateFunction(llvmReturnType, parameterTypes);
        var function = _module.AddFunction("main", type);
        var block = function.AppendBasicBlock("entry");
        _builder.PositionAtEnd(block);
        var returnValue = _builder.BuildCall2(runFunctionType, runFunction, []);
        if (returnType is LoweredPrimitiveDataType { Primitive: Primitive.Int32 })
        {
            _builder.BuildRet(returnValue);
        }
        else
        {
            _builder.BuildRet(LLVMValueRef.CreateConstInt(llvmReturnType, 0));
        }
    }

    private LLVMValueRef Visit(LoweredConstStructNode node)
    {
        var structDataType = (LoweredStructDataType)node.DataType;
        var type = _typeBuilder.BuildType(node.DataType);
        var name = ((LoweredStructDataType)node.DataType).Name;
        var values = node
            .Values
            .Select(x => Next(x)!.Value)
            .ToArray();

        return name == null
            ? LLVMValueRef.CreateConstStruct(values, Packed: false)
            : LLVMValueRef.CreateConstNamedStruct(type, values);
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
}
