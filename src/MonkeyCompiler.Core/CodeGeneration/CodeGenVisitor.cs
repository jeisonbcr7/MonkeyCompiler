using System.Reflection;
using System.Reflection.Emit;
using MonkeyCompiler.Core.Ast;

namespace MonkeyCompiler.Core.CodeGeneration;

internal sealed record VariableInfo(string Name, Type Type, LocalBuilder? LocalBuilder, int ParameterIndex = -1)
{
    public bool IsParameter => ParameterIndex >= 0;
}

internal sealed record FunctionInfo(string Name, MethodBuilder Method, Type ReturnType, Type[] ParameterTypes);

internal sealed class TypeMapper
{
    public Type Map(TypeNode typeNode)
    {
        return typeNode switch
        {
            IntTypeNode => typeof(int),
            BoolTypeNode => typeof(bool),
            StringTypeNode => typeof(string),
            CharTypeNode => typeof(char),
            VoidTypeNode => typeof(void),
            ArrayTypeNode => typeof(List<object?>),
            HashTypeNode => typeof(Dictionary<object?, object?>),
            FunctionTypeNode => typeof(Delegate),
            _ => typeof(object)
        };
    }
}

internal sealed class CodeGenVisitor
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly TypeBuilder _typeBuilder;
    private readonly TypeMapper _typeMapper = new();

    private readonly Dictionary<string, FunctionInfo> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MethodInfo> _builtIns = new(StringComparer.Ordinal);
    private readonly Stack<Dictionary<string, VariableInfo>> _scopes = new();

    public CodeGenVisitor(ModuleBuilder moduleBuilder, TypeBuilder typeBuilder)
    {
        _moduleBuilder = moduleBuilder;
        _typeBuilder = typeBuilder;
        RegisterBuiltIns();
    }

    public MethodBuilder EmitProgram(ProgramNode program)
    {
        DeclareFunctions(program);

        foreach (var function in program.Functions)
        {
            EmitFunction(function);
        }

        EmitFunction(program.MainFunction);

        var entryPoint = _typeBuilder.DefineMethod(
            "Main",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            new[] { typeof(string[]) });

        var il = entryPoint.GetILGenerator();
        _scopes.Push(new());
        EmitStatements(program.Statements, il);
        if (_functions.TryGetValue(program.MainFunction.Name, out var mainInfo))
        {
            EmitCall(il, mainInfo.Method);
            if (mainInfo.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
        }
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        _scopes.Pop();

        return entryPoint;
    }

    private void RegisterBuiltIns()
    {
        _builtIns["len"] = typeof(RuntimeBuiltIns).GetMethod(nameof(RuntimeBuiltIns.Len))!;
        _builtIns["first"] = typeof(RuntimeBuiltIns).GetMethod(nameof(RuntimeBuiltIns.First))!;
        _builtIns["last"] = typeof(RuntimeBuiltIns).GetMethod(nameof(RuntimeBuiltIns.Last))!;
        _builtIns["rest"] = typeof(RuntimeBuiltIns).GetMethod(nameof(RuntimeBuiltIns.Rest))!;
        _builtIns["push"] = typeof(RuntimeBuiltIns).GetMethod(nameof(RuntimeBuiltIns.Push))!;
    }

    private void DeclareFunctions(ProgramNode program)
    {
        foreach (var function in program.Functions.Append(program.MainFunction))
        {
            var parameterTypes = function.Parameters.Select(p => _typeMapper.Map(p.Type)).ToArray();
            var returnType = _typeMapper.Map(function.ReturnType);

            var method = _typeBuilder.DefineMethod(
                function.Name,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                parameterTypes);

            _functions[function.Name] = new FunctionInfo(function.Name, method, returnType, parameterTypes);
        }
    }

    private void EmitFunction(FunctionDeclarationNode function)
    {
        var info = _functions[function.Name];
        var il = info.Method.GetILGenerator();

        _scopes.Push(new());

        for (int i = 0; i < function.Parameters.Count; i++)
        {
            var parameter = function.Parameters[i];
            var parameterType = _typeMapper.Map(parameter.Type);
            _scopes.Peek()[parameter.Name] = new VariableInfo(parameter.Name, parameterType, null, i);
        }

        EmitBlock(function.Body, il);

        if (info.ReturnType == typeof(void))
        {
            il.Emit(OpCodes.Ret);
        }

        _scopes.Pop();
    }

    private void EmitBlock(BlockStatementNode block, ILGenerator il)
    {
        _scopes.Push(new());
        EmitStatements(block.Statements, il);
        _scopes.Pop();
    }

    private void EmitStatements(IReadOnlyList<StatementNode> statements, ILGenerator il)
    {
        foreach (var statement in statements)
        {
            EmitStatement(statement, il);
        }
    }

    private void EmitStatement(StatementNode statement, ILGenerator il)
    {
        switch (statement)
        {
            case BlockStatementNode block:
                EmitBlock(block, il);
                break;
            case LetStatementNode let:
                EmitLet(let, il);
                break;
            case ReturnStatementNode ret:
                EmitReturn(ret, il);
                break;
            case ExpressionStatementNode expressionStatement:
                EmitExpression(expressionStatement.Expression, il);
                il.Emit(OpCodes.Pop);
                break;
            case IfStatementNode ifStatement:
                EmitIf(ifStatement, il);
                break;
            case PrintStatementNode printStatement:
                EmitPrint(printStatement, il);
                break;
            default:
                throw new NotSupportedException($"Sentencia no soportada: {statement.GetType().Name}");
        }
    }

    private void EmitPrint(PrintStatementNode printStatement, ILGenerator il)
    {
        var type = EmitExpression(printStatement.Expression, il);
        var method = typeof(Console).GetMethod("WriteLine", new[] { type }) ?? typeof(Console).GetMethod("WriteLine", new[] { typeof(object) });
        if (type.IsValueType && method!.GetParameters()[0].ParameterType == typeof(object))
        {
            il.Emit(OpCodes.Box, type);
        }
        il.Emit(OpCodes.Call, method!);
    }

    private void EmitIf(IfStatementNode ifStatement, ILGenerator il)
    {
        var conditionType = EmitExpression(ifStatement.Condition, il);
        if (conditionType != typeof(bool))
        {
            il.Emit(OpCodes.Conv_I1);
        }

        var elseLabel = il.DefineLabel();
        var endLabel = il.DefineLabel();

        il.Emit(OpCodes.Brfalse, elseLabel);
        EmitBlock(ifStatement.Consequence, il);
        il.Emit(OpCodes.Br, endLabel);

        il.MarkLabel(elseLabel);
        if (ifStatement.Alternative is not null)
        {
            EmitBlock(ifStatement.Alternative, il);
        }

        il.MarkLabel(endLabel);
    }

    private void EmitReturn(ReturnStatementNode ret, ILGenerator il)
    {
        if (ret.Expression is not null)
        {
            EmitExpression(ret.Expression, il);
        }

        il.Emit(OpCodes.Ret);
    }

    private void EmitLet(LetStatementNode let, ILGenerator il)
    {
        var targetType = _typeMapper.Map(let.Type);
        var local = il.DeclareLocal(targetType);
        _scopes.Peek()[let.Name] = new VariableInfo(let.Name, targetType, local);

        var valueType = EmitExpression(let.Value, il);
        if (valueType != targetType)
        {
            if (valueType.IsValueType && targetType == typeof(object))
            {
                il.Emit(OpCodes.Box, valueType);
            }
            else if (targetType.IsValueType && valueType == typeof(object))
            {
                il.Emit(OpCodes.Unbox_Any, targetType);
            }
        }

        il.Emit(OpCodes.Stloc, local);
    }

    private Type EmitExpression(ExpressionNode expression, ILGenerator il)
    {
        switch (expression)
        {
            case IdentifierExpressionNode identifier:
                return EmitIdentifier(identifier, il);
            case IntegerLiteralNode integerLiteral:
                il.Emit(OpCodes.Ldc_I4, integerLiteral.Value);
                return typeof(int);
            case StringLiteralNode stringLiteral:
                il.Emit(OpCodes.Ldstr, stringLiteral.Value);
                return typeof(string);
            case BooleanLiteralNode booleanLiteral:
                il.Emit(booleanLiteral.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                return typeof(bool);
            case CharLiteralNode charLiteral:
                il.Emit(OpCodes.Ldc_I4, charLiteral.Value);
                il.Emit(OpCodes.Conv_U2);
                return typeof(char);
            case ArrayLiteralNode arrayLiteral:
                return EmitArrayLiteral(arrayLiteral, il);
            case HashLiteralNode hashLiteral:
                return EmitHashLiteral(hashLiteral, il);
            case FunctionLiteralNode:
                throw new NotSupportedException("Las funciones anónimas no están soportadas en esta versión.");
            case CallExpressionNode callExpression:
                return EmitCallExpression(callExpression, il);
            case IndexExpressionNode indexExpression:
                return EmitIndexExpression(indexExpression, il);
            case InfixExpressionNode infixExpression:
                return EmitInfixExpression(infixExpression, il);
            default:
                throw new NotSupportedException($"Expresión no soportada: {expression.GetType().Name}");
        }
    }

    private Type EmitIdentifier(IdentifierExpressionNode identifier, ILGenerator il)
    {
        var variable = LookupVariable(identifier.Name);
        if (variable is not null)
        {
            if (variable.IsParameter)
            {
                il.Emit(OpCodes.Ldarg, variable.ParameterIndex);
            }
            else if (variable.LocalBuilder is not null)
            {
                il.Emit(OpCodes.Ldloc, variable.LocalBuilder);
            }

            return variable.Type;
        }

        throw new InvalidOperationException($"Identificador no encontrado: {identifier.Name}");
    }

    private Type EmitArrayLiteral(ArrayLiteralNode arrayLiteral, ILGenerator il)
    {
        var listType = typeof(List<object?>);
        var ctor = listType.GetConstructor(Type.EmptyTypes)!;
        var addMethod = listType.GetMethod("Add", new[] { typeof(object) })!;

        var local = il.DeclareLocal(listType);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Stloc, local);

        foreach (var element in arrayLiteral.Elements)
        {
            il.Emit(OpCodes.Ldloc, local);
            var elementType = EmitExpression(element, il);
            if (elementType.IsValueType)
            {
                il.Emit(OpCodes.Box, elementType);
            }

            il.Emit(OpCodes.Callvirt, addMethod);
        }

        il.Emit(OpCodes.Ldloc, local);
        return listType;
    }

    private Type EmitHashLiteral(HashLiteralNode hashLiteral, ILGenerator il)
    {
        var dictType = typeof(Dictionary<object?, object?>);
        var ctor = dictType.GetConstructor(Type.EmptyTypes)!;
        var addMethod = dictType.GetMethod("Add", new[] { typeof(object), typeof(object) })!;

        var local = il.DeclareLocal(dictType);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Stloc, local);

        foreach (var pair in hashLiteral.Pairs)
        {
            il.Emit(OpCodes.Ldloc, local);
            var keyType = EmitExpression(pair.Key, il);
            if (keyType.IsValueType)
            {
                il.Emit(OpCodes.Box, keyType);
            }

            var valueType = EmitExpression(pair.Value, il);
            if (valueType.IsValueType)
            {
                il.Emit(OpCodes.Box, valueType);
            }

            il.Emit(OpCodes.Callvirt, addMethod);
        }

        il.Emit(OpCodes.Ldloc, local);
        return dictType;
    }

    private Type EmitCallExpression(CallExpressionNode callExpression, ILGenerator il)
    {
        if (callExpression.Function is IdentifierExpressionNode identifier && _builtIns.TryGetValue(identifier.Name, out var builtIn))
        {
            var parameterTypes = builtIn.GetParameters().Select(p => p.ParameterType).ToArray();
            for (int i = 0; i < callExpression.Arguments.Count; i++)
            {
                var argumentType = EmitExpression(callExpression.Arguments[i], il);
                if (argumentType.IsValueType && parameterTypes[i] == typeof(object))
                {
                    il.Emit(OpCodes.Box, argumentType);
                }
            }

            il.Emit(OpCodes.Call, builtIn);
            return builtIn.ReturnType;
        }

        if (callExpression.Function is IdentifierExpressionNode functionIdentifier && _functions.TryGetValue(functionIdentifier.Name, out var functionInfo))
        {
            var parameterTypes = functionInfo.ParameterTypes;
            for (int i = 0; i < callExpression.Arguments.Count; i++)
            {
                var argumentType = EmitExpression(callExpression.Arguments[i], il);
                if (argumentType.IsValueType && parameterTypes[i] == typeof(object))
                {
                    il.Emit(OpCodes.Box, argumentType);
                }
            }

            EmitCall(il, functionInfo.Method);
            return functionInfo.ReturnType;
        }

        throw new NotSupportedException("Sólo se soportan llamadas a funciones declaradas o built-ins.");
    }

    private void EmitCall(ILGenerator il, MethodInfo method)
    {
        il.Emit(OpCodes.Call, method);
    }

    private Type EmitIndexExpression(IndexExpressionNode indexExpression, ILGenerator il)
    {
        var targetType = EmitExpression(indexExpression.Target, il);
        var targetLocal = il.DeclareLocal(targetType);
        il.Emit(OpCodes.Stloc, targetLocal);

        var indexType = EmitExpression(indexExpression.Index, il);
        if (indexType != typeof(int))
        {
            il.Emit(OpCodes.Conv_I4);
        }

        var indexLocal = il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Stloc, indexLocal);

        il.Emit(OpCodes.Ldloc, targetLocal);
        il.Emit(OpCodes.Ldloc, indexLocal);

        if (targetType == typeof(List<object?>))
        {
            var itemGetter = targetType.GetProperty("Item")!.GetMethod!;
            il.Emit(OpCodes.Callvirt, itemGetter);
            return typeof(object);
        }

        if (targetType == typeof(Dictionary<object?, object?>))
        {
            var indexer = targetType.GetProperty("Item")!.GetMethod!;
            if (indexType.IsValueType)
            {
                il.Emit(OpCodes.Box, indexType);
            }
            il.Emit(OpCodes.Callvirt, indexer);
            return typeof(object);
        }

        throw new NotSupportedException("Indexación no soportada para el tipo de destino.");
    }

    private Type EmitInfixExpression(InfixExpressionNode infixExpression, ILGenerator il)
    {
        var leftType = EmitExpression(infixExpression.Left, il);
        var rightType = EmitExpression(infixExpression.Right, il);

        var op = infixExpression.Operator;
        switch (op)
        {
            case "+":
                if (leftType == typeof(string) || rightType == typeof(string))
                {
                    if (leftType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, leftType);
                    }
                    if (rightType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, rightType);
                    }
                    il.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) })!);
                    return typeof(string);
                }

                il.Emit(OpCodes.Add);
                return typeof(int);
            case "-":
                il.Emit(OpCodes.Sub);
                return typeof(int);
            case "*":
                il.Emit(OpCodes.Mul);
                return typeof(int);
            case "/":
                il.Emit(OpCodes.Div);
                return typeof(int);
            case "<":
                il.Emit(OpCodes.Clt);
                return typeof(bool);
            case ">":
                il.Emit(OpCodes.Cgt);
                return typeof(bool);
            case "==":
                il.Emit(OpCodes.Ceq);
                return typeof(bool);
            case "!=":
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ceq);
                return typeof(bool);
            case "&&":
                il.Emit(OpCodes.And);
                return typeof(bool);
            case "||":
                il.Emit(OpCodes.Or);
                return typeof(bool);
            default:
                throw new NotSupportedException($"Operador no soportado: {op}");
        }
    }

    private VariableInfo? LookupVariable(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var variable))
            {
                return variable;
            }
        }

        return null;
    }
}
