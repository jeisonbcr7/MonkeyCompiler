using MonkeyCompiler.Core.Ast;

namespace MonkeyCompiler.Core.Semantics;

public sealed class TypeCheckingVisitor
{
    private readonly List<string> _errors = new();
    private readonly SymbolTable _symbolTable = new();
    private readonly Stack<TypeDescriptor> _returnTypes = new();

    public TypeCheckResult Check(ProgramNode program)
    {
        _errors.Clear();
        _symbolTable.Clear();
        _symbolTable.PushScope();
        RegisterBuiltIns();

        DeclareFunctions(program.Functions);
        DeclareFunction(program.MainFunction);

        foreach (var statement in program.Statements)
        {
            CheckStatement(statement, expectedReturnType: null);
        }

        foreach (var function in program.Functions)
        {
            CheckFunction(function);
        }

        CheckFunction(program.MainFunction);

        _symbolTable.PopScope();

        return new TypeCheckResult(_errors.Count == 0, _errors.ToArray());
    }

    private void DeclareFunctions(IEnumerable<FunctionDeclarationNode> functions)
    {
        foreach (var function in functions)
        {
            DeclareFunction(function);
        }
    }

    private void DeclareFunction(FunctionDeclarationNode function)
    {
        var functionType = ResolveType(function.ReturnType);
        var parameterTypes = function.Parameters.Select(p => ResolveType(p.Type)).ToArray();
        functionType = TypeDescriptor.Function(parameterTypes, functionType);

        if (!_symbolTable.TryDeclare(new Symbol(function.Name, functionType, SymbolCategory.Function)))
        {
            AddError(function.Location, $"La función '{function.Name}' ya está declarada en este ámbito.");
        }
    }

    private void RegisterBuiltIns()
    {
        DeclareBuiltIn("len", TypeDescriptor.Function(new[] { TypeDescriptor.Any }, TypeDescriptor.Int));
        DeclareBuiltIn("first", TypeDescriptor.Function(new[] { TypeDescriptor.Any }, TypeDescriptor.Any));
        DeclareBuiltIn("last", TypeDescriptor.Function(new[] { TypeDescriptor.Any }, TypeDescriptor.Any));
        DeclareBuiltIn("rest", TypeDescriptor.Function(new[] { TypeDescriptor.Any }, TypeDescriptor.Any));
        DeclareBuiltIn("push", TypeDescriptor.Function(new[] { TypeDescriptor.Any, TypeDescriptor.Any }, TypeDescriptor.Any));
    }

    private void DeclareBuiltIn(string name, TypeDescriptor type)
    {
        _symbolTable.TryDeclare(new Symbol(name, type, SymbolCategory.BuiltIn));
    }

    private void CheckFunction(FunctionDeclarationNode function)
    {
        _symbolTable.PushScope();
        var parameterTypes = new List<TypeDescriptor>();

        foreach (var parameter in function.Parameters)
        {
            var parameterType = ResolveType(parameter.Type);
            parameterTypes.Add(parameterType);

            if (!_symbolTable.TryDeclare(new Symbol(parameter.Name, parameterType, SymbolCategory.Variable, isConst: false, isParameter: true)))
            {
                AddError(parameter.Location, $"El parámetro '{parameter.Name}' ya está declarado en este ámbito.");
            }
        }

        var returnType = ResolveType(function.ReturnType);
        _returnTypes.Push(returnType);
        CheckBlock(function.Body);
        _returnTypes.Pop();

        _symbolTable.PopScope();
    }

    private void CheckBlock(BlockStatementNode block)
    {
        _symbolTable.PushScope();
        foreach (var statement in block.Statements)
        {
            CheckStatement(statement, _returnTypes.Count > 0 ? _returnTypes.Peek() : null);
        }

        _symbolTable.PopScope();
    }

    private void CheckStatement(StatementNode statement, TypeDescriptor? expectedReturnType)
    {
        switch (statement)
        {
            case BlockStatementNode block:
                CheckBlock(block);
                break;
            case LetStatementNode let:
                CheckLetStatement(let);
                break;
            case ReturnStatementNode ret:
                CheckReturnStatement(ret, expectedReturnType);
                break;
            case ExpressionStatementNode expressionStatement:
                CheckExpression(expressionStatement.Expression);
                break;
            case IfStatementNode ifStatement:
                CheckIfStatement(ifStatement, expectedReturnType);
                break;
            case PrintStatementNode printStatement:
                CheckExpression(printStatement.Expression);
                break;
            default:
                AddError(statement.Location, "Sentencia no soportada para análisis de tipos.");
                break;
        }
    }

    private void CheckIfStatement(IfStatementNode ifStatement, TypeDescriptor? expectedReturnType)
    {
        var conditionType = CheckExpression(ifStatement.Condition);
        if (!AreCompatible(TypeDescriptor.Bool, conditionType))
        {
            AddError(ifStatement.Condition.Location, $"La condición del if debe ser de tipo bool, se encontró {conditionType}.");
        }

        CheckBlock(ifStatement.Consequence);

        if (ifStatement.Alternative is not null)
        {
            CheckBlock(ifStatement.Alternative);
        }
    }

    private void CheckReturnStatement(ReturnStatementNode ret, TypeDescriptor? expectedReturnType)
    {
        if (expectedReturnType is null)
        {
            AddError(ret.Location, "'return' solo puede usarse dentro de una función.");
            return;
        }

        if (expectedReturnType.Equals(TypeDescriptor.Void))
        {
            if (ret.Expression is not null)
            {
                AddError(ret.Location, "La función void no puede devolver un valor.");
            }
            return;
        }

        if (ret.Expression is null)
        {
            AddError(ret.Location, $"La función debe devolver un valor de tipo {expectedReturnType}.");
            return;
        }

        var actualType = CheckExpression(ret.Expression);
        if (!AreCompatible(expectedReturnType, actualType))
        {
            AddError(ret.Expression.Location, $"El tipo de retorno esperado es {expectedReturnType}, se encontró {actualType}.");
        }
    }

    private void CheckLetStatement(LetStatementNode let)
    {
        var declaredType = ResolveType(let.Type);
        if (declaredType.Equals(TypeDescriptor.Void))
        {
            AddError(let.Location, "No se puede declarar una variable de tipo void.");
        }

        if (!_symbolTable.TryDeclare(new Symbol(let.Name, declaredType, SymbolCategory.Variable, let.IsConst)))
        {
            AddError(let.Location, $"El identificador '{let.Name}' ya está declarado en este ámbito.");
        }

        var valueType = CheckExpression(let.Value);
        if (!AreCompatible(declaredType, valueType))
        {
            AddError(let.Value.Location, $"El valor asignado a '{let.Name}' debe ser de tipo {declaredType}, se encontró {valueType}.");
        }
    }

    private TypeDescriptor CheckExpression(ExpressionNode expression)
    {
        return expression switch
        {
            IdentifierExpressionNode identifier => CheckIdentifier(identifier),
            IntegerLiteralNode => TypeDescriptor.Int,
            StringLiteralNode => TypeDescriptor.String,
            BooleanLiteralNode => TypeDescriptor.Bool,
            CharLiteralNode => TypeDescriptor.Char,
            ArrayLiteralNode array => CheckArrayLiteral(array),
            HashLiteralNode hash => CheckHashLiteral(hash),
            FunctionLiteralNode function => CheckFunctionLiteral(function),
            CallExpressionNode call => CheckCallExpression(call),
            IndexExpressionNode index => CheckIndexExpression(index),
            InfixExpressionNode infix => CheckInfixExpression(infix),
            _ => TypeDescriptor.Any
        };
    }

    private TypeDescriptor CheckIdentifier(IdentifierExpressionNode identifier)
    {
        if (!_symbolTable.TryResolve(identifier.Name, out var symbol))
        {
            AddError(identifier.Location, $"El identificador '{identifier.Name}' se usa antes de ser declarado.");
            return TypeDescriptor.Any;
        }

        return symbol!.Type;
    }

    private TypeDescriptor CheckArrayLiteral(ArrayLiteralNode array)
    {
        if (array.Elements.Count == 0)
        {
            return TypeDescriptor.Array(TypeDescriptor.Any);
        }

        var elementTypes = array.Elements.Select(CheckExpression).ToArray();
        var firstType = elementTypes[0];

        foreach (var elementType in elementTypes.Skip(1))
        {
            if (!AreCompatible(firstType, elementType))
            {
                AddError(array.Location, "Todos los elementos del arreglo deben ser del mismo tipo.");
                break;
            }
        }

        return TypeDescriptor.Array(firstType);
    }

    private TypeDescriptor CheckHashLiteral(HashLiteralNode hash)
    {
        if (hash.Pairs.Count == 0)
        {
            return TypeDescriptor.Hash(TypeDescriptor.Any, TypeDescriptor.Any);
        }

        var keyTypes = new List<TypeDescriptor>();
        var valueTypes = new List<TypeDescriptor>();

        foreach (var pair in hash.Pairs)
        {
            keyTypes.Add(CheckExpression(pair.Key));
            valueTypes.Add(CheckExpression(pair.Value));
        }

        var firstKey = keyTypes[0];
        var firstValue = valueTypes[0];

        if (keyTypes.Any(t => !AreCompatible(firstKey, t)))
        {
            AddError(hash.Location, "Todas las llaves del hash deben ser del mismo tipo.");
        }

        if (valueTypes.Any(t => !AreCompatible(firstValue, t)))
        {
            AddError(hash.Location, "Todos los valores del hash deben ser del mismo tipo.");
        }

        return TypeDescriptor.Hash(firstKey, firstValue);
    }

    private TypeDescriptor CheckFunctionLiteral(FunctionLiteralNode function)
    {
        _symbolTable.PushScope();

        var parameterTypes = new List<TypeDescriptor>();
        foreach (var parameter in function.Parameters)
        {
            var parameterType = ResolveType(parameter.Type);
            parameterTypes.Add(parameterType);

            if (!_symbolTable.TryDeclare(new Symbol(parameter.Name, parameterType, SymbolCategory.Variable, isConst: false, isParameter: true)))
            {
                AddError(parameter.Location, $"El parámetro '{parameter.Name}' ya está declarado en este ámbito.");
            }
        }

        var returnType = ResolveType(function.ReturnType);
        _returnTypes.Push(returnType);
        CheckBlock(function.Body);
        _returnTypes.Pop();

        _symbolTable.PopScope();

        return TypeDescriptor.Function(parameterTypes, returnType);
    }

    private TypeDescriptor CheckCallExpression(CallExpressionNode call)
    {
        if (call.Function is IdentifierExpressionNode identifier &&
            _symbolTable.TryResolve(identifier.Name, out var symbol) &&
            symbol!.Category == SymbolCategory.BuiltIn)
        {
            return ValidateBuiltInCall(identifier, call.Arguments);
        }

        var functionType = CheckExpression(call.Function);
        if (functionType.Kind != TypeKind.Function)
        {
            AddError(call.Location, "Solo se pueden invocar expresiones de tipo función.");
            return TypeDescriptor.Any;
        }

        if (functionType.ParameterTypes.Count != call.Arguments.Count)
        {
            AddError(call.Location, $"La función espera {functionType.ParameterTypes.Count} argumento(s), se pasaron {call.Arguments.Count}.");
        }

        var parameterCount = Math.Min(functionType.ParameterTypes.Count, call.Arguments.Count);
        for (var i = 0; i < parameterCount; i++)
        {
            var argumentType = CheckExpression(call.Arguments[i]);
            var expectedType = functionType.ParameterTypes[i];

            if (!AreCompatible(expectedType, argumentType))
            {
                AddError(call.Arguments[i].Location, $"El argumento {i + 1} debe ser de tipo {expectedType}, se encontró {argumentType}.");
            }
        }

        return functionType.ReturnType!;
    }

    private TypeDescriptor ValidateBuiltInCall(IdentifierExpressionNode identifier, IReadOnlyList<ExpressionNode> arguments)
    {
        var name = identifier.Name;

        return name switch
        {
            "len" => ValidateLen(arguments),
            "first" => ValidateArrayBuiltIn(arguments, "first", returnElement: true),
            "last" => ValidateArrayBuiltIn(arguments, "last", returnElement: true),
            "rest" => ValidateArrayBuiltIn(arguments, "rest", returnElement: false),
            "push" => ValidatePush(arguments),
            _ => TypeDescriptor.Any
        };
    }

    private TypeDescriptor ValidateLen(IReadOnlyList<ExpressionNode> arguments)
    {
        if (arguments.Count != 1)
        {
            AddError(arguments.Count > 0 ? arguments[0].Location : new SourceLocation(0, 0), "len espera exactamente 1 argumento.");
            return TypeDescriptor.Int;
        }

        var argumentType = CheckExpression(arguments[0]);
        if (argumentType.Kind != TypeKind.Array && !argumentType.Equals(TypeDescriptor.String))
        {
            AddError(arguments[0].Location, "len solo puede aplicarse a arreglos o strings.");
        }

        return TypeDescriptor.Int;
    }

    private TypeDescriptor ValidateArrayBuiltIn(IReadOnlyList<ExpressionNode> arguments, string name, bool returnElement)
    {
        if (arguments.Count != 1)
        {
            AddError(arguments.Count > 0 ? arguments[0].Location : new SourceLocation(0, 0), $"{name} espera exactamente 1 argumento.");
            return TypeDescriptor.Any;
        }

        var argumentType = CheckExpression(arguments[0]);
        if (argumentType.Kind != TypeKind.Array)
        {
            AddError(arguments[0].Location, $"{name} solo puede aplicarse a arreglos.");
            return TypeDescriptor.Any;
        }

        return returnElement ? argumentType.ElementType! : argumentType;
    }

    private TypeDescriptor ValidatePush(IReadOnlyList<ExpressionNode> arguments)
    {
        if (arguments.Count != 2)
        {
            AddError(arguments.Count > 0 ? arguments[0].Location : new SourceLocation(0, 0), "push espera 2 argumentos.");
            return TypeDescriptor.Any;
        }

        var arrayType = CheckExpression(arguments[0]);
        var valueType = CheckExpression(arguments[1]);

        if (arrayType.Kind != TypeKind.Array)
        {
            AddError(arguments[0].Location, "El primer argumento de push debe ser un arreglo.");
            return TypeDescriptor.Any;
        }

        if (!AreCompatible(arrayType.ElementType!, valueType))
        {
            AddError(arguments[1].Location, $"El elemento a insertar debe ser de tipo {arrayType.ElementType}, se encontró {valueType}.");
        }

        return arrayType;
    }

    private TypeDescriptor CheckIndexExpression(IndexExpressionNode index)
    {
        var targetType = CheckExpression(index.Target);
        var indexType = CheckExpression(index.Index);

        if (targetType.Kind == TypeKind.Array)
        {
            if (!AreCompatible(TypeDescriptor.Int, indexType))
            {
                AddError(index.Index.Location, "El índice de un arreglo debe ser de tipo int.");
            }

            return targetType.ElementType!;
        }

        if (targetType.Kind == TypeKind.Hash)
        {
            if (!AreCompatible(targetType.KeyType!, indexType))
            {
                AddError(index.Index.Location, $"La llave del hash debe ser de tipo {targetType.KeyType}, se encontró {indexType}.");
            }

            return targetType.ValueType!;
        }

        if (targetType.Equals(TypeDescriptor.String))
        {
            if (!AreCompatible(TypeDescriptor.Int, indexType))
            {
                AddError(index.Index.Location, "El índice de un string debe ser de tipo int.");
            }

            return TypeDescriptor.Char;
        }

        AddError(index.Target.Location, "Solo se pueden indexar arreglos, hashes o strings.");
        return TypeDescriptor.Any;
    }

    private TypeDescriptor CheckInfixExpression(InfixExpressionNode infix)
    {
        var leftType = CheckExpression(infix.Left);
        var rightType = CheckExpression(infix.Right);

        switch (infix.Operator)
        {
            case "+":
            case "-":
            case "*":
            case "/":
                if (AreCompatible(TypeDescriptor.Int, leftType) && AreCompatible(TypeDescriptor.Int, rightType))
                {
                    return TypeDescriptor.Int;
                }

                if (infix.Operator == "+" && AreCompatible(TypeDescriptor.String, leftType) && AreCompatible(TypeDescriptor.String, rightType))
                {
                    return TypeDescriptor.String;
                }

                AddError(infix.Location, "Los operadores aritméticos solo aceptan int (o strings para concatenación).");
                return TypeDescriptor.Any;

            case "<":
            case "<=":
            case ">":
            case ">=":
                if (!AreCompatible(TypeDescriptor.Int, leftType) || !AreCompatible(TypeDescriptor.Int, rightType))
                {
                    AddError(infix.Location, "Las comparaciones requieren operandos int.");
                }
                return TypeDescriptor.Bool;

            case "==":
            case "!=":
                if (!AreCompatible(leftType, rightType))
                {
                    AddError(infix.Location, "Solo se pueden comparar valores del mismo tipo.");
                }
                return TypeDescriptor.Bool;
            default:
                AddError(infix.Location, $"Operador '{infix.Operator}' no soportado.");
                return TypeDescriptor.Any;
        }
    }

    private TypeDescriptor ResolveType(TypeNode type)
    {
        return type switch
        {
            IntTypeNode => TypeDescriptor.Int,
            StringTypeNode => TypeDescriptor.String,
            BoolTypeNode => TypeDescriptor.Bool,
            CharTypeNode => TypeDescriptor.Char,
            VoidTypeNode => TypeDescriptor.Void,
            ArrayTypeNode array => TypeDescriptor.Array(ResolveType(array.ElementType)),
            HashTypeNode hash => TypeDescriptor.Hash(ResolveType(hash.KeyType), ResolveType(hash.ValueType)),
            FunctionTypeNode function => TypeDescriptor.Function(function.ParameterTypes.Select(ResolveType).ToArray(), ResolveType(function.ReturnType)),
            _ => TypeDescriptor.Any
        };
    }

    private bool AreCompatible(TypeDescriptor expected, TypeDescriptor actual)
    {
        if (expected.Kind == TypeKind.Any || actual.Kind == TypeKind.Any)
        {
            return true;
        }

        return expected.Equals(actual);
    }

    private void AddError(SourceLocation location, string message)
    {
        _errors.Add($"[L{location.Line}, C{location.Column}] {message}");
    }
}
