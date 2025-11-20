using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Generated; // MonkeyParser, MonkeyParserBaseVisitor, MonkeyLexer
using MonkeyCompiler.Semantics;


namespace MonkeyCompiler.Semantics
{
    public class TypeCheckException : Exception
    {
        public TypeCheckException() { }
    }

    public class MonkeyTypeChecker : MonkeyParserBaseVisitor<MonkeyType>
    {
        private readonly SymbolTable _symbols = new SymbolTable();
        private readonly List<string> _errors = new();
        private readonly Stack<MonkeyType?> _functionStack = new(); // función actual (para returns)

        private MonkeyType? CurrentFunction => _functionStack.Count > 0 ? _functionStack.Peek() : null;

        public bool HasErrors => _errors.Count > 0;
        public IEnumerable<string> Errors => _errors;

        private void ReportError(string msg, IToken token)
        {
            _errors.Add(
                $"TYPE ERROR - line {token.Line}:{token.Column + 1} {msg} (\"{token.Text}\")"
            );
        }

        private void ReportError(string msg, MonkeyType expected, MonkeyType actual, IToken token)
        {
            _errors.Add(
                $"TYPE ERROR - line {token.Line}:{token.Column + 1} {msg} (expected {expected}, found {actual})"
            );
        }

        // ================== Helpers de tipos ==================

        private bool IsError(MonkeyType t) => t.Kind == BaseTypeKind.Error;

        private bool IsInt(MonkeyType t) => t.Kind == BaseTypeKind.Int;
        private bool IsBool(MonkeyType t) => t.Kind == BaseTypeKind.Bool;
        private bool IsString(MonkeyType t) => t.Kind == BaseTypeKind.String;
        private bool IsChar(MonkeyType t) => t.Kind == BaseTypeKind.Char;
        private bool IsVoid(MonkeyType t) => t.Kind == BaseTypeKind.Void;

        private bool AreSameType(MonkeyType a, MonkeyType b)
        {
            if (a.Kind == BaseTypeKind.Error || b.Kind == BaseTypeKind.Error) return true;
            if (a.Kind != b.Kind) return false;

            // array
            if (a.ElementType != null || b.ElementType != null)
            {
                if (a.ElementType == null || b.ElementType == null) return false;
                return AreSameType(a.ElementType, b.ElementType);
            }

            // hash
            if (a.KeyType != null || b.KeyType != null || a.ValueType != null || b.ValueType != null)
            {
                if (a.KeyType == null || b.KeyType == null || a.ValueType == null || b.ValueType == null)
                    return false;
                return AreSameType(a.KeyType, b.KeyType) && AreSameType(a.ValueType, b.ValueType);
            }

            // function
            if (a.ParameterTypes != null || b.ParameterTypes != null)
            {
                if (a.ParameterTypes == null || b.ParameterTypes == null) return false;
                if (a.ParameterTypes.Count != b.ParameterTypes.Count) return false;
                for (int i = 0; i < a.ParameterTypes.Count; i++)
                {
                    if (!AreSameType(a.ParameterTypes[i], b.ParameterTypes[i])) return false;
                }
                if (a.ReturnType == null || b.ReturnType == null) return false;
                return AreSameType(a.ReturnType, b.ReturnType);
            }

            return true;
        }

        private bool IsAssignable(MonkeyType target, MonkeyType source)
        {
            // Por ahora, "asignable" == "igual"
            return AreSameType(target, source);
        }

        // ================== Built-ins (len, first, last, etc.) ==================

        private void InsertBuiltins()
        {
            // len(x) : int  donde x puede ser array<*> o string
            var lenType = MonkeyType.FunctionOf(
                new List<MonkeyType> { MonkeyType.Any() },
                MonkeyType.Int()
            );
            _symbols.Declare(new Symbol("len", lenType, isConst: true, isBuiltin: true));

            // first(a) : any   a debe ser array<*>
            var firstType = MonkeyType.FunctionOf(
                new List<MonkeyType> { MonkeyType.Any() },
                MonkeyType.Any()
            );
            _symbols.Declare(new Symbol("first", firstType, isConst: true, isBuiltin: true));

            // last(a) : any   a debe ser array<*>
            var lastType = MonkeyType.FunctionOf(
                new List<MonkeyType> { MonkeyType.Any() },
                MonkeyType.Any()
            );
            _symbols.Declare(new Symbol("last", lastType, isConst: true, isBuiltin: true));

            // Puedes agregar otros built-ins (rest, push, etc.) si el profe los quiere.
        }

        // ================== Tipos desde la gramática (type, arrayType, etc.) ==================

        public override MonkeyType VisitType(MonkeyParser.TypeContext ctx)
        {
            if (ctx.TYPE_INT() != null) return MonkeyType.Int();
            if (ctx.TYPE_STRING() != null) return MonkeyType.String();
            if (ctx.TYPE_BOOL() != null) return MonkeyType.Bool();
            if (ctx.TYPE_CHAR() != null) return MonkeyType.Char();
            if (ctx.TYPE_VOID() != null) return MonkeyType.Void();
            if (ctx.arrayType() != null) return VisitArrayType(ctx.arrayType());
            if (ctx.hashType() != null) return VisitHashType(ctx.hashType());
            if (ctx.functionType() != null) return VisitFunctionType(ctx.functionType());

            return MonkeyType.Error();
        }

        public override MonkeyType VisitArrayType(MonkeyParser.ArrayTypeContext ctx)
        {
            var elemType = Visit(ctx.type());
            return MonkeyType.ArrayOf(elemType);
        }

        public override MonkeyType VisitHashType(MonkeyParser.HashTypeContext ctx)
        {
            var keyType = Visit(ctx.type(0));
            var valueType = Visit(ctx.type(1));
            return MonkeyType.HashOf(keyType, valueType);
        }

        public override MonkeyType VisitFunctionType(MonkeyParser.FunctionTypeContext ctx)
        {
            var paramTypes = new List<MonkeyType>();
            if (ctx.functionParameterTypes() != null)
            {
                foreach (var t in ctx.functionParameterTypes().type())
                {
                    paramTypes.Add(Visit(t));
                }
            }
            var returnType = Visit(ctx.type());
            return MonkeyType.FunctionOf(paramTypes, returnType);
        }

        // ================== Programa (dos pasadas) ==================

        public override MonkeyType VisitProgram(MonkeyParser.ProgramContext ctx)
        {
            // Ámbito global ya existe en SymbolTable
            InsertBuiltins();

            // Primera pasada: registrar firmas de funciones (incluyendo main)
            foreach (var child in ctx.children)
            {
                if (child is MonkeyParser.FunctionDeclarationContext fdecl)
                {
                    RegisterFunctionSignature(fdecl);
                }
                else if (child is MonkeyParser.MainFunctionContext mctx)
                {
                    RegisterMainSignature(mctx);
                }
            }

            // Segunda pasada: chequear cuerpos
            return base.VisitProgram(ctx);
        }

        private void RegisterFunctionSignature(MonkeyParser.FunctionDeclarationContext ctx)
        {
            string name = ctx.IDENTIFIER().GetText();
            var paramTypes = new List<MonkeyType>();

            if (ctx.functionParameters() != null)
            {
                foreach (var p in ctx.functionParameters().parameter())
                {
                    paramTypes.Add(Visit(p.type()));
                }
            }
            MonkeyType retType = Visit(ctx.type());
            var funcType = MonkeyType.FunctionOf(paramTypes, retType);

            var sym = new Symbol(name, funcType, isConst: true);
            if (!_symbols.Declare(sym))
            {
                ReportError($"Identifier '{name}' already declared in this scope", ctx.IDENTIFIER().Symbol);
            }
        }

        private void RegisterMainSignature(MonkeyParser.MainFunctionContext ctx)
        {
            string name = "main";
            var funcType = MonkeyType.FunctionOf(new List<MonkeyType>(), MonkeyType.Void());

            var sym = new Symbol(name, funcType, isConst: true);
            if (!_symbols.Declare(sym))
            {
                ReportError("main function already declared", ctx.MAIN().Symbol);
            }
        }

        // ================== Funciones ==================

        public override MonkeyType VisitFunctionDeclaration(MonkeyParser.FunctionDeclarationContext ctx)
        {
            string name = ctx.IDENTIFIER().GetText();
            var funcSym = _symbols.Resolve(name);
            MonkeyType funcType = funcSym?.Type ?? MonkeyType.Error();

            _functionStack.Push(funcType);
            _symbols.OpenScope();

            // parámetros
            if (ctx.functionParameters() != null)
            {
                int i = 0;
                foreach (var p in ctx.functionParameters().parameter())
                {
                    string pname = p.IDENTIFIER().GetText();
                    MonkeyType pType = Visit(p.type());

                    // coherencia con la firma
                    if (funcType.ParameterTypes != null &&
                        i < funcType.ParameterTypes.Count &&
                        !AreSameType(funcType.ParameterTypes[i], pType))
                    {
                        ReportError(
                            $"Parameter '{pname}' type does not match declared function type",
                            funcType.ParameterTypes[i],
                            pType,
                            p.IDENTIFIER().Symbol
                        );
                    }

                    var psym = new Symbol(pname, pType, isConst: false);
                    if (!_symbols.Declare(psym))
                    {
                        ReportError($"Parameter '{pname}' already declared in this scope", p.IDENTIFIER().Symbol);
                    }
                    i++;
                }
            }

            // cuerpo
            Visit(ctx.blockStatement());

            _symbols.CloseScope();
            _functionStack.Pop();

            return funcType;
        }

        public override MonkeyType VisitMainFunction(MonkeyParser.MainFunctionContext ctx)
        {
            // main() : void  ya está registrado
            var mainSym = _symbols.Resolve("main");
            MonkeyType mainType = mainSym?.Type ?? MonkeyType.FunctionOf(new List<MonkeyType>(), MonkeyType.Void());

            _functionStack.Push(mainType);
            _symbols.OpenScope();

            // no tiene parámetros
            Visit(ctx.blockStatement());

            _symbols.CloseScope();
            _functionStack.Pop();

            return mainType;
        }

        // ================== Statements ==================

        public override MonkeyType VisitBlockStatement(MonkeyParser.BlockStatementContext ctx)
        {
            _symbols.OpenScope();

            foreach (var st in ctx.statement())
            {
                Visit(st);
            }

            _symbols.CloseScope();
            return MonkeyType.Void();
        }

        public override MonkeyType VisitLetStatement(MonkeyParser.LetStatementContext ctx)
        {
            string name = ctx.IDENTIFIER().GetText();
            bool isConst = ctx.CONST() != null;

            MonkeyType declaredType = Visit(ctx.type());
            MonkeyType exprType = Visit(ctx.expression());

            // Regla 3: tipos compatibles en la asignación
            if (!IsAssignable(declaredType, exprType))
            {
                ReportError($"Incompatible types in let assignment to '{name}'",
                    declaredType, exprType, ctx.ASSIGN().Symbol);
            }

            // Para hashLiteral: queremos conservar las KnownKeys del RHS
            MonkeyType symbolType = exprType.Clone();
            // Pero garantizamos que su estructura es la declarada
            symbolType.Kind = declaredType.Kind;
            symbolType.ElementType = declaredType.ElementType ?? symbolType.ElementType;
            symbolType.KeyType = declaredType.KeyType ?? symbolType.KeyType;
            symbolType.ValueType = declaredType.ValueType ?? symbolType.ValueType;
            symbolType.ReturnType = declaredType.ReturnType ?? symbolType.ReturnType;
            symbolType.ParameterTypes = declaredType.ParameterTypes ?? symbolType.ParameterTypes;

            var sym = new Symbol(name, symbolType, isConst);
            if (!_symbols.Declare(sym))
            {
                // Regla 1: no redeclarar en el mismo ámbito
                ReportError($"Identifier '{name}' already declared in this scope", ctx.IDENTIFIER().Symbol);
            }

            return MonkeyType.Void();
        }

        public override MonkeyType VisitReturnStatement(MonkeyParser.ReturnStatementContext ctx)
        {
            if (CurrentFunction == null)
            {
                // Regla 6: no retornar valor fuera de función
                ReportError("Return statement not allowed outside of a function", ctx.RETURN().Symbol);
                return MonkeyType.Error();
            }

            MonkeyType funcRet = CurrentFunction.ReturnType ?? MonkeyType.Void();

            if (ctx.expression() == null)
            {
                // return;  -> solo permitido en funciones void
                if (!IsVoid(funcRet))
                {
                    ReportError("Missing return value in non-void function", ctx.RETURN().Symbol);
                }
                return MonkeyType.Void();
            }
            else
            {
                MonkeyType exprType = Visit(ctx.expression());

                // Regla 7: tipo de retorno debe ser igual al tipo declarado
                if (!IsAssignable(funcRet, exprType))
                {
                    ReportError("Return expression type does not match function return type",
                        funcRet, exprType, ctx.RETURN().Symbol);
                }

                return funcRet;
            }
        }

        public override MonkeyType VisitExpressionStatement(MonkeyParser.ExpressionStatementContext ctx)
        {
            return Visit(ctx.expression());
        }

        public override MonkeyType VisitIfStatement(MonkeyParser.IfStatementContext ctx)
        {
            MonkeyType condType = Visit(ctx.expression());
            if (!IsBool(condType))
            {
                ReportError("Condition in if-statement must be of type bool", ctx.IF().Symbol);
            }

            Visit(ctx.blockStatement(0));
            if (ctx.blockStatement().Length > 1)
            {
                Visit(ctx.blockStatement(1));
            }

            return MonkeyType.Void();
        }

        public override MonkeyType VisitPrintStatement(MonkeyParser.PrintStatementContext ctx)
        {
            MonkeyType exprType = Visit(ctx.expression());
            // Podrías prohibir imprimir void si quieres
            if (IsVoid(exprType))
            {
                ReportError("Cannot print value of type void", ctx.PRINT().Symbol);
            }
            return MonkeyType.Void();
        }

        // ================== Expresiones ==================

        public override MonkeyType VisitExpression(MonkeyParser.ExpressionContext ctx)
        {
            var left = Visit(ctx.additionExpression());
            if (ctx.comparison() == null) return left;

            var comp = ctx.comparison();
            var rightExprs = comp.additionExpression();

            // hijos de comparison: op, expr, op, expr, ...
            MonkeyType current = left;
            int pairCount = rightExprs.Length;
            for (int i = 0; i < pairCount; i++)
            {
                var right = Visit(rightExprs[i]);
                // operador está en posición 2*i
                var opNode = comp.GetChild(2 * i);
                string opText = opNode.GetText();
                IToken opToken = (opNode.Payload as IToken) ?? comp.Start;

                current = CheckComparison(opText, current, right, opToken);
            }

            // resultado de comparación siempre bool (si no hubo error)
            return current;
        }

        private MonkeyType CheckComparison(string op, MonkeyType left, MonkeyType right, IToken token)
        {
            if (IsError(left) || IsError(right)) return MonkeyType.Error();

            switch (op)
            {
                case "<":
                case "<=":
                case ">":
                case ">=":
                    if (IsInt(left) && IsInt(right))
                        return MonkeyType.Bool();
                    ReportError("Relational operators require int operands", token);
                    return MonkeyType.Error();

                case "==":
                case "!=":
                    if (AreSameType(left, right))
                        return MonkeyType.Bool();
                    ReportError("Equality operators require operands of the same type", token);
                    return MonkeyType.Error();

                default:
                    ReportError($"Unknown comparison operator '{op}'", token);
                    return MonkeyType.Error();
            }
        }

        public override MonkeyType VisitAdditionExpression(MonkeyParser.AdditionExpressionContext ctx)
        {
            var mults = ctx.multiplicationExpression();
            MonkeyType current = Visit(mults[0]);

            int count = mults.Length;
            for (int i = 1; i < count; i++)
            {
                // hijos: mult0, op1, mult1, op2, mult2...
                var opNode = ctx.GetChild(2 * i - 1);
                string opText = opNode.GetText();
                IToken opToken = (opNode.Payload as IToken) ?? ctx.Start;

                var right = Visit(mults[i]);
                current = CheckAdditive(opText, current, right, opToken);
            }

            return current;
        }

        private MonkeyType CheckAdditive(string op, MonkeyType left, MonkeyType right, IToken token)
        {
            if (IsError(left) || IsError(right)) return MonkeyType.Error();

            switch (op)
            {
                case "+":
                case "-":
                    // Por ahora, solo int op int -> int
                    if (IsInt(left) && IsInt(right))
                        return MonkeyType.Int();

                    // (opcional) concatenación de strings con +
                    if (op == "+" && IsString(left) && IsString(right))
                        return MonkeyType.String();

                    ReportError($"Operator '{op}' requires numeric (or string for +) operands", token);
                    return MonkeyType.Error();

                default:
                    ReportError($"Unknown additive operator '{op}'", token);
                    return MonkeyType.Error();
            }
        }

        public override MonkeyType VisitMultiplicationExpression(MonkeyParser.MultiplicationExpressionContext ctx)
        {
            var elems = ctx.elementExpression();
            MonkeyType current = Visit(elems[0]);
            int count = elems.Length;

            for (int i = 1; i < count; i++)
            {
                var opNode = ctx.GetChild(2 * i - 1);
                string opText = opNode.GetText();
                IToken opToken = (opNode.Payload as IToken) ?? ctx.Start;

                var right = Visit(elems[i]);
                current = CheckMultiplicative(opText, current, right, opToken);
            }

            return current;
        }

        private MonkeyType CheckMultiplicative(string op, MonkeyType left, MonkeyType right, IToken token)
        {
            if (IsError(left) || IsError(right)) return MonkeyType.Error();

            switch (op)
            {
                case "*":
                case "/":
                    if (IsInt(left) && IsInt(right))
                        return MonkeyType.Int();
                    ReportError($"Operator '{op}' requires int operands", token);
                    return MonkeyType.Error();

                default:
                    ReportError($"Unknown multiplicative operator '{op}'", token);
                    return MonkeyType.Error();
            }
        }

        public override MonkeyType VisitElementExpression(MonkeyParser.ElementExpressionContext ctx)
        {
            var baseType = Visit(ctx.primitiveExpression());

            if (ctx.elementAccess() != null)
            {
                var acc = ctx.elementAccess();
                var indexType = Visit(acc.expression());

                // array access
                if (baseType.ElementType != null)
                {
                    // index debe ser int
                    if (!IsInt(indexType))
                    {
                        ReportError("Array index must be of type int", acc.LBRACKET().Symbol);
                        return MonkeyType.Error();
                    }
                    return baseType.ElementType;
                }

                // hash access
                if (baseType.KeyType != null && baseType.ValueType != null)
                {
                    // Regla 8: hashContent solo int/string como claves ya se revisa en hashLiteral
                    // Aquí: tipo de índice compatible con tipo de clave
                    if (!AreSameType(baseType.KeyType, indexType))
                    {
                        ReportError("Hash index type must match hash key type",
                            baseType.KeyType!, indexType, acc.LBRACKET().Symbol);
                        return MonkeyType.Error();
                    }

                    // Regla 9: si podemos, verificar que el campo exista
                    // Solo si la expresión base es un identificador con KnownKeys y el índice es constante
                    string? baseName = ctx.primitiveExpression().GetText();
                    var baseSym = _symbols.Resolve(baseName);
                    if (baseSym != null && baseSym.Type.KnownKeys != null)
                    {
                        string indexText = acc.expression().GetText();
                        if (!baseSym.Type.KnownKeys.Contains(indexText) &&
                            (IsInt(indexType) || IsString(indexType)))
                        {
                            ReportError($"Hash key '{indexText}' does not exist in hash literal", acc.LBRACKET().Symbol);
                            return MonkeyType.Error();
                        }
                    }

                    return baseType.ValueType!;
                }

                ReportError("Type is not indexable (neither array nor hash)", acc.LBRACKET().Symbol);
                return MonkeyType.Error();
            }

            if (ctx.callExpression() != null)
            {
                var call = ctx.callExpression();
                var funcType = baseType;

                // Solo se puede llamar a funciones
                if (funcType.ParameterTypes == null || funcType.ReturnType == null)
                {
                    ReportError("Attempting to call a non-function value", call.LPAREN().Symbol);
                    return MonkeyType.Error();
                }

                // argumentos
                var argTypes = new List<MonkeyType>();
                if (call.expressionList() != null)
                {
                    foreach (var e in call.expressionList().expression())
                    {
                        argTypes.Add(Visit(e));
                    }
                }

                // Regla 5: número y tipo de parámetros
                if (funcType.ParameterTypes.Count != argTypes.Count)
                {
                    ReportError(
                        $"Function expects {funcType.ParameterTypes.Count} arguments but {argTypes.Count} were provided",
                        call.LPAREN().Symbol);
                }
                else
                {
                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        if (!IsAssignable(funcType.ParameterTypes[i], argTypes[i]))
                        {
                            ReportError(
                                $"Argument {i + 1} type does not match parameter type",
                                funcType.ParameterTypes[i],
                                argTypes[i],
                                call.LPAREN().Symbol
                            );
                        }
                    }
                }

                return funcType.ReturnType;
            }

            return baseType;
        }

        // ================== PrimitiveExpression y literales ==================

        public override MonkeyType VisitPrimitiveExpression(MonkeyParser.PrimitiveExpressionContext ctx)
        {
            if (ctx.INTEGER() != null) return MonkeyType.Int();
            if (ctx.STRING() != null) return MonkeyType.String();
            if (ctx.CHAR() != null) return MonkeyType.Char();
            if (ctx.TRUE() != null || ctx.FALSE() != null) return MonkeyType.Bool();

            if (ctx.IDENTIFIER() != null)
            {
                string name = ctx.IDENTIFIER().GetText();
                var sym = _symbols.Resolve(name);
                if (sym == null)
                {
                    // Regla 2: identificador usado sin declarar
                    ReportError($"Identifier '{name}' used before declaration", ctx.IDENTIFIER().Symbol);
                    return MonkeyType.Error();
                }
                return sym.Type;
            }

            if (ctx.expression() != null)
            {
                // ( expression )
                return Visit(ctx.expression());
            }

            if (ctx.arrayLiteral() != null) return VisitArrayLiteral(ctx.arrayLiteral());
            if (ctx.functionLiteral() != null) return VisitFunctionLiteral(ctx.functionLiteral());
            if (ctx.hashLiteral() != null) return VisitHashLiteral(ctx.hashLiteral());

            return MonkeyType.Error();
        }

        public override MonkeyType VisitArrayLiteral(MonkeyParser.ArrayLiteralContext ctx)
        {
            if (ctx.expressionList() == null)
            {
                // Arreglo vacío -> no podemos inferir tipo fácilmente, puedes marcar error o usar any
                ReportError("Cannot infer type of empty array literal", ctx.LBRACKET().Symbol);
                return MonkeyType.Error();
            }

            MonkeyType? elementType = null;
            foreach (var e in ctx.expressionList().expression())
            {
                var t = Visit(e);
                if (IsError(t)) continue;

                if (elementType == null)
                {
                    elementType = t;
                }
                else if (!AreSameType(elementType, t))
                {
                    ReportError("All elements in array literal must have the same type", e.Start);
                    elementType = MonkeyType.Error();
                }
            }

            if (elementType == null || IsError(elementType))
                return MonkeyType.Error();

            return MonkeyType.ArrayOf(elementType);
        }

        public override MonkeyType VisitFunctionLiteral(MonkeyParser.FunctionLiteralContext ctx)
        {
            var paramTypes = new List<MonkeyType>();
            if (ctx.functionParameters() != null)
            {
                foreach (var p in ctx.functionParameters().parameter())
                {
                    paramTypes.Add(Visit(p.type()));
                }
            }
            MonkeyType retType = Visit(ctx.type());
            var funcType = MonkeyType.FunctionOf(paramTypes, retType);

            _functionStack.Push(funcType);
            _symbols.OpenScope();

            // parámetros como variables dentro del cuerpo
            if (ctx.functionParameters() != null)
            {
                int i = 0;
                foreach (var p in ctx.functionParameters().parameter())
                {
                    string pname = p.IDENTIFIER().GetText();
                    MonkeyType pType = Visit(p.type());

                    // coherencia con tipo de función
                    if (!AreSameType(pType, paramTypes[i]))
                    {
                        ReportError(
                            $"Parameter '{pname}' type does not match function literal type",
                            paramTypes[i],
                            pType,
                            p.IDENTIFIER().Symbol
                        );
                    }

                    var psym = new Symbol(pname, pType, isConst: false);
                    if (!_symbols.Declare(psym))
                    {
                        ReportError($"Parameter '{pname}' already declared in this scope", p.IDENTIFIER().Symbol);
                    }
                    i++;
                }
            }

            Visit(ctx.blockStatement());

            _symbols.CloseScope();
            _functionStack.Pop();

            return funcType;
        }

        public override MonkeyType VisitHashLiteral(MonkeyParser.HashLiteralContext ctx)
        {
            MonkeyType? keyType = null;
            MonkeyType? valueType = null;
            var knownKeys = new HashSet<string>();

            foreach (var hc in ctx.hashContent())
            {
                var kExpr = hc.expression(0);
                var vExpr = hc.expression(1);

                MonkeyType kType = Visit(kExpr);
                MonkeyType vType = Visit(vExpr);

                // Regla 8: claves solo int o string
                if (!(IsInt(kType) || IsString(kType)))
                {
                    ReportError("Hash key type must be int or string", kExpr.Start);
                }

                if (keyType == null)
                {
                    keyType = kType;
                    valueType = vType;
                }
                else
                {
                    if (!AreSameType(keyType, kType))
                    {
                        ReportError("All keys in hash literal must have the same type", kExpr.Start);
                    }
                    if (!AreSameType(valueType!, vType))
                    {
                        ReportError("All values in hash literal must have the same type", vExpr.Start);
                    }
                }

                // Almacenar clave conocida si es literal constante (número o string)
                string keyText = kExpr.GetText();
                knownKeys.Add(keyText);
            }

            if (keyType == null || valueType == null)
                return MonkeyType.Error();

            var hashType = MonkeyType.HashOf(keyType, valueType);
            hashType.KnownKeys = knownKeys;
            return hashType;
        }

        // expressionList solo se usa para evaluar subexpresiones, el tipo lo deciden los que llaman
        public override MonkeyType VisitExpressionList(MonkeyParser.ExpressionListContext ctx)
        {
            foreach (var e in ctx.expression())
            {
                Visit(e);
            }
            return MonkeyType.Void();
        }

        public override string ToString()
        {
            if (!HasErrors) return "0 errors";
            return string.Join(Environment.NewLine, _errors);
        }
    }
}
