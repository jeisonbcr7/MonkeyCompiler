namespace MonkeyCompiler.Core.Semantics;

public sealed class SymbolTable
{
    private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

    public void PushScope()
    {
        _scopes.Push(new Dictionary<string, Symbol>());
    }

    public void PopScope()
    {
        _scopes.Pop();
    }

    public void Clear()
    {
        _scopes.Clear();
    }

    public bool TryDeclare(Symbol symbol)
    {
        var current = _scopes.Peek();
        if (current.ContainsKey(symbol.Name))
        {
            return false;
        }

        current.Add(symbol.Name, symbol);
        return true;
    }

    public bool TryResolve(string name, out Symbol? symbol)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out symbol))
            {
                return true;
            }
        }

        symbol = null;
        return false;
    }
}
