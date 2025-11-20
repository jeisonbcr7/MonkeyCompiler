using System.Collections.Generic;

namespace MonkeyCompiler.Semantics
{
    public class Symbol
    {
        public string Name { get; }
        public MonkeyType Type { get; set; }
        public bool IsConst { get; }
        public bool IsBuiltin { get; }

        public Symbol(string name, MonkeyType type, bool isConst = false, bool isBuiltin = false)
        {
            Name = name;
            Type = type;
            IsConst = isConst;
            IsBuiltin = isBuiltin;
        }

        public bool IsFunction => Type.ParameterTypes != null && Type.ReturnType != null;
        public bool IsArray => Type.ElementType != null;
        public bool IsHash => Type.KeyType != null && Type.ValueType != null;
    }

    public class SymbolTable
    {
        private readonly List<Dictionary<string, Symbol>> _scopes = new();

        public SymbolTable()
        {
            // ámbito global
            _scopes.Add(new Dictionary<string, Symbol>());
        }

        private Dictionary<string, Symbol> CurrentScope => _scopes[_scopes.Count - 1];

        public void OpenScope()
        {
            _scopes.Add(new Dictionary<string, Symbol>());
        }

        public void CloseScope()
        {
            if (_scopes.Count > 1)
                _scopes.RemoveAt(_scopes.Count - 1);
        }

        /// <summary>
        /// Declara un símbolo en el ámbito actual.
        /// Devuelve false si ya existe un símbolo con ese nombre en el mismo ámbito.
        /// </summary>
        public bool Declare(Symbol symbol)
        {
            if (CurrentScope.ContainsKey(symbol.Name))
            {
                return false;
            }
            CurrentScope[symbol.Name] = symbol;
            return true;
        }

        /// <summary>
        /// Busca un símbolo en todos los ámbitos (de más interno a global).
        /// </summary>
        public Symbol? Resolve(string name)
        {
            for (int i = _scopes.Count - 1; i >= 0; i--)
            {
                if (_scopes[i].TryGetValue(name, out var sym))
                    return sym;
            }
            return null;
        }

        /// <summary>
        /// Busca solo en el ámbito actual.
        /// </summary>
        public Symbol? ResolveCurrent(string name)
        {
            if (CurrentScope.TryGetValue(name, out var sym))
                return sym;
            return null;
        }
    }
}
