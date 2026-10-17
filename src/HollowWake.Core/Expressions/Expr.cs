using System.Collections.Concurrent;
using System.Globalization;

namespace HollowWake.Core.Expressions;

/// <summary>
/// Anything an expression evaluates against: an entity, a weave, a holding, a test scope.
/// Identifiers resolve to numbers; flags answer <c>has("burning")</c>; randomness is injected so
/// evaluation stays deterministic under a seeded <see cref="Maths.Rng"/>.
/// </summary>
public interface IExprScope
{
    bool TryResolve(string name, out double value);
    bool HasFlag(string flag);
    double NextRandom();
}

public sealed class ExprParseException : Exception
{
    public int Position { get; }

    public ExprParseException(string message, int position) : base($"{message} (at {position})")
    {
        Position = position;
    }
}

public sealed class ExprEvalException : Exception
{
    public ExprEvalException(string message) : base(message)
    {
    }
}

/// <summary>
/// A compiled formula. The content layer writes numbers and rules as text
/// (<c>"40 + level * 8 + strength * 2"</c>, <c>"has(\"burning\") ? 1.5 : 1"</c>) and the engine
/// evaluates them against a scope. Parsing is cached by source text.
/// </summary>
public sealed class Expr
{
    private static readonly ConcurrentDictionary<string, Expr> Cache = new(StringComparer.Ordinal);

    private readonly Node _root;

    public string Source { get; }

    /// <summary>Every identifier the expression reads. Used by validators to check references.</summary>
    public IReadOnlyList<string> Identifiers { get; }

    /// <summary>Every flag queried through <c>has("...")</c>.</summary>
    public IReadOnlyList<string> Flags { get; }

    public bool IsConstant => _root is ConstNode;

    public double ConstantValue => _root is ConstNode c ? c.Value : throw new InvalidOperationException("Expression is not constant.");

    private Expr(string source, Node root)
    {
        Source = source;
        _root = root;
        var ids = new List<string>();
        var flags = new List<string>();
        root.Collect(ids, flags);
        Identifiers = ids.Distinct(StringComparer.Ordinal).ToArray();
        Flags = flags.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static Expr Constant(double value) => new(value.ToString("R", CultureInfo.InvariantCulture), new ConstNode(value));

    public static Expr Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Cache.GetOrAdd(source, static s => new Expr(s, new Parser(s).ParseRoot()));
    }

    public static bool TryParse(string source, out Expr? expr, out string? error)
    {
        try
        {
            expr = Parse(source);
            error = null;
            return true;
        }
        catch (ExprParseException ex)
        {
            expr = null;
            error = ex.Message;
            return false;
        }
    }

    public double Evaluate(IExprScope scope) => _root.Eval(scope);

    public bool IsTrue(IExprScope scope) => Evaluate(scope) != 0;

    public override string ToString() => Source;

    // ---------------------------------------------------------------- AST

    private abstract class Node
    {
        public abstract double Eval(IExprScope scope);
        public abstract void Collect(List<string> identifiers, List<string> flags);
    }

    private sealed class ConstNode : Node
    {
        public double Value { get; }
        public ConstNode(double v) => Value = v;
        public override double Eval(IExprScope scope) => Value;
        public override void Collect(List<string> identifiers, List<string> flags) { }
    }

    private sealed class StringNode : Node
    {
        public string Value { get; }
        public StringNode(string v) => Value = v;
        public override double Eval(IExprScope scope) => throw new ExprEvalException($"String \"{Value}\" used where a number was expected.");
        public override void Collect(List<string> identifiers, List<string> flags) { }
    }

    private sealed class VarNode : Node
    {
        public string Name { get; }
        public VarNode(string name) => Name = name;

        public override double Eval(IExprScope scope)
        {
            if (scope.TryResolve(Name, out var v))
            {
                return v;
            }
            throw new ExprEvalException($"Unknown identifier '{Name}'.");
        }

        public override void Collect(List<string> identifiers, List<string> flags) => identifiers.Add(Name);
    }

    private sealed class UnaryNode : Node
    {
        private readonly char _op;
        private readonly Node _a;
        public UnaryNode(char op, Node a) { _op = op; _a = a; }

        public override double Eval(IExprScope scope) => _op switch
        {
            '-' => -_a.Eval(scope),
            '!' => _a.Eval(scope) == 0 ? 1 : 0,
            _ => throw new ExprEvalException($"Unknown unary operator {_op}.")
        };

        public override void Collect(List<string> identifiers, List<string> flags) => _a.Collect(identifiers, flags);
    }

    private sealed class BinaryNode : Node
    {
        private readonly string _op;
        private readonly Node _a;
        private readonly Node _b;
        public BinaryNode(string op, Node a, Node b) { _op = op; _a = a; _b = b; }

        public override double Eval(IExprScope scope)
        {
            switch (_op)
            {
                case "&&": return _a.Eval(scope) != 0 && _b.Eval(scope) != 0 ? 1 : 0;
                case "||": return _a.Eval(scope) != 0 || _b.Eval(scope) != 0 ? 1 : 0;
            }
            var x = _a.Eval(scope);
            var y = _b.Eval(scope);
            return _op switch
            {
                "+" => x + y,
                "-" => x - y,
                "*" => x * y,
                "/" => y == 0 ? 0 : x / y,
                "%" => y == 0 ? 0 : x % y,
                "^" => Math.Pow(x, y),
                "<" => x < y ? 1 : 0,
                "<=" => x <= y ? 1 : 0,
                ">" => x > y ? 1 : 0,
                ">=" => x >= y ? 1 : 0,
                "==" => Math.Abs(x - y) < 1e-9 ? 1 : 0,
                "!=" => Math.Abs(x - y) >= 1e-9 ? 1 : 0,
                _ => throw new ExprEvalException($"Unknown operator {_op}.")
            };
        }

        public override void Collect(List<string> identifiers, List<string> flags)
        {
            _a.Collect(identifiers, flags);
            _b.Collect(identifiers, flags);
        }
    }

    private sealed class TernaryNode : Node
    {
        private readonly Node _c, _a, _b;
        public TernaryNode(Node c, Node a, Node b) { _c = c; _a = a; _b = b; }
        public override double Eval(IExprScope scope) => _c.Eval(scope) != 0 ? _a.Eval(scope) : _b.Eval(scope);

        public override void Collect(List<string> identifiers, List<string> flags)
        {
            _c.Collect(identifiers, flags);
            _a.Collect(identifiers, flags);
            _b.Collect(identifiers, flags);
        }
    }

    private sealed class CallNode : Node
    {
        private readonly string _name;
        private readonly Node[] _args;
        private readonly ExprFunctions.Function? _fn;

        public CallNode(string name, Node[] args, int position)
        {
            _name = name;
            _args = args;
            switch (name)
            {
                case "has":
                    if (args.Length != 1 || args[0] is not StringNode)
                    {
                        throw new ExprParseException("has() takes exactly one quoted flag name", position);
                    }
                    break;
                case "rand":
                    if (args.Length != 0)
                    {
                        throw new ExprParseException("rand() takes no arguments", position);
                    }
                    break;
                case "if":
                    if (args.Length != 3)
                    {
                        throw new ExprParseException("if() takes condition, then, else", position);
                    }
                    break;
                default:
                    if (!ExprFunctions.TryGet(name, out _fn))
                    {
                        throw new ExprParseException($"Unknown function '{name}'", position);
                    }
                    if (args.Length < _fn!.MinArgs || args.Length > _fn.MaxArgs)
                    {
                        throw new ExprParseException($"{name}() expects {_fn.MinArgs}..{_fn.MaxArgs} arguments, got {args.Length}", position);
                    }
                    break;
            }
        }

        public override double Eval(IExprScope scope)
        {
            switch (_name)
            {
                case "has": return scope.HasFlag(((StringNode)_args[0]).Value) ? 1 : 0;
                case "rand": return scope.NextRandom();
                case "if": return _args[0].Eval(scope) != 0 ? _args[1].Eval(scope) : _args[2].Eval(scope);
            }
            var values = new double[_args.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = _args[i].Eval(scope);
            }
            return _fn!.Impl(values);
        }

        public override void Collect(List<string> identifiers, List<string> flags)
        {
            if (_name == "has")
            {
                flags.Add(((StringNode)_args[0]).Value);
                return;
            }
            foreach (var a in _args)
            {
                a.Collect(identifiers, flags);
            }
        }
    }

    // ---------------------------------------------------------------- Parser

    private sealed class Parser
    {
        private enum Tok { Number, Ident, String, Op, LParen, RParen, Comma, Question, Colon, End }

        private readonly string _s;
        private int _pos;
        private Tok _tok;
        private string _text = "";
        private double _num;
        private int _tokStart;

        public Parser(string s)
        {
            _s = s;
            Advance();
        }

        public Node ParseRoot()
        {
            if (_tok == Tok.End)
            {
                throw new ExprParseException("Empty expression", 0);
            }
            var n = ParseTernary();
            if (_tok != Tok.End)
            {
                throw new ExprParseException($"Unexpected '{_text}'", _tokStart);
            }
            return Fold(n);
        }

        private static Node Fold(Node n) => n;

        private Node ParseTernary()
        {
            var c = ParseOr();
            if (_tok == Tok.Question)
            {
                Advance();
                var a = ParseTernary();
                Expect(Tok.Colon, ":");
                var b = ParseTernary();
                return new TernaryNode(c, a, b);
            }
            return c;
        }

        private Node ParseOr()
        {
            var n = ParseAnd();
            while (_tok == Tok.Op && _text == "||")
            {
                Advance();
                n = new BinaryNode("||", n, ParseAnd());
            }
            return n;
        }

        private Node ParseAnd()
        {
            var n = ParseCompare();
            while (_tok == Tok.Op && _text == "&&")
            {
                Advance();
                n = new BinaryNode("&&", n, ParseCompare());
            }
            return n;
        }

        private Node ParseCompare()
        {
            var n = ParseAdd();
            while (_tok == Tok.Op && _text is "<" or "<=" or ">" or ">=" or "==" or "!=")
            {
                var op = _text;
                Advance();
                n = new BinaryNode(op, n, ParseAdd());
            }
            return n;
        }

        private Node ParseAdd()
        {
            var n = ParseMul();
            while (_tok == Tok.Op && _text is "+" or "-")
            {
                var op = _text;
                Advance();
                n = new BinaryNode(op, n, ParseMul());
            }
            return n;
        }

        private Node ParseMul()
        {
            var n = ParseUnary();
            while (_tok == Tok.Op && _text is "*" or "/" or "%")
            {
                var op = _text;
                Advance();
                n = new BinaryNode(op, n, ParseUnary());
            }
            return n;
        }

        private Node ParseUnary()
        {
            if (_tok == Tok.Op && _text is "-" or "!")
            {
                var op = _text[0];
                Advance();
                var operand = ParseUnary();
                if (op == '-' && operand is ConstNode c)
                {
                    return new ConstNode(-c.Value);
                }
                return new UnaryNode(op, operand);
            }
            return ParsePow();
        }

        private Node ParsePow()
        {
            var n = ParsePrimary();
            if (_tok == Tok.Op && _text == "^")
            {
                Advance();
                var exponent = ParseUnary();
                return new BinaryNode("^", n, exponent);
            }
            return n;
        }

        private Node ParsePrimary()
        {
            switch (_tok)
            {
                case Tok.Number:
                {
                    var v = _num;
                    Advance();
                    return new ConstNode(v);
                }
                case Tok.String:
                {
                    var v = _text;
                    Advance();
                    return new StringNode(v);
                }
                case Tok.Ident:
                {
                    var name = _text;
                    var start = _tokStart;
                    Advance();
                    if (_tok == Tok.LParen)
                    {
                        Advance();
                        var args = new List<Node>();
                        if (_tok != Tok.RParen)
                        {
                            args.Add(ParseTernary());
                            while (_tok == Tok.Comma)
                            {
                                Advance();
                                args.Add(ParseTernary());
                            }
                        }
                        Expect(Tok.RParen, ")");
                        return new CallNode(name, args.ToArray(), start);
                    }
                    if (name == "true")
                    {
                        return new ConstNode(1);
                    }
                    if (name == "false")
                    {
                        return new ConstNode(0);
                    }
                    return new VarNode(name);
                }
                case Tok.LParen:
                {
                    Advance();
                    var inner = ParseTernary();
                    Expect(Tok.RParen, ")");
                    return inner;
                }
                default:
                    throw new ExprParseException(_tok == Tok.End ? "Unexpected end of expression" : $"Unexpected '{_text}'", _tokStart);
            }
        }

        private void Expect(Tok tok, string what)
        {
            if (_tok != tok)
            {
                throw new ExprParseException($"Expected '{what}'", _tokStart);
            }
            Advance();
        }

        private void Advance()
        {
            while (_pos < _s.Length && char.IsWhiteSpace(_s[_pos]))
            {
                _pos++;
            }
            _tokStart = _pos;
            if (_pos >= _s.Length)
            {
                _tok = Tok.End;
                _text = "";
                return;
            }
            var c = _s[_pos];
            if (char.IsDigit(c) || (c == '.' && _pos + 1 < _s.Length && char.IsDigit(_s[_pos + 1])))
            {
                var start = _pos;
                while (_pos < _s.Length && (char.IsDigit(_s[_pos]) || _s[_pos] == '.'))
                {
                    _pos++;
                }
                if (_pos < _s.Length && (_s[_pos] == 'e' || _s[_pos] == 'E'))
                {
                    var save = _pos;
                    _pos++;
                    if (_pos < _s.Length && (_s[_pos] == '+' || _s[_pos] == '-'))
                    {
                        _pos++;
                    }
                    if (_pos < _s.Length && char.IsDigit(_s[_pos]))
                    {
                        while (_pos < _s.Length && char.IsDigit(_s[_pos]))
                        {
                            _pos++;
                        }
                    }
                    else
                    {
                        _pos = save;
                    }
                }
                var text = _s.Substring(start, _pos - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _num))
                {
                    throw new ExprParseException($"Bad number '{text}'", start);
                }
                // "20%" is a percent literal (0.2) when the sign touches the number.
                if (_pos < _s.Length && _s[_pos] == '%')
                {
                    _pos++;
                    _num /= 100.0;
                }
                _tok = Tok.Number;
                _text = text;
                return;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = _pos;
                while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_' || _s[_pos] == '.'))
                {
                    _pos++;
                }
                _text = _s.Substring(start, _pos - start);
                _tok = Tok.Ident;
                return;
            }
            if (c == '"' || c == '\'')
            {
                var quote = c;
                var start = ++_pos;
                while (_pos < _s.Length && _s[_pos] != quote)
                {
                    _pos++;
                }
                if (_pos >= _s.Length)
                {
                    throw new ExprParseException("Unterminated string", start - 1);
                }
                _text = _s.Substring(start, _pos - start);
                _pos++;
                _tok = Tok.String;
                return;
            }
            _pos++;
            switch (c)
            {
                case '(': _tok = Tok.LParen; _text = "("; return;
                case ')': _tok = Tok.RParen; _text = ")"; return;
                case ',': _tok = Tok.Comma; _text = ","; return;
                case '?': _tok = Tok.Question; _text = "?"; return;
                case ':': _tok = Tok.Colon; _text = ":"; return;
                case '+': case '-': case '*': case '/': case '%': case '^':
                    _tok = Tok.Op; _text = c.ToString(); return;
                case '<': case '>': case '=': case '!':
                    if (_pos < _s.Length && _s[_pos] == '=')
                    {
                        _pos++;
                        _text = c + "=";
                    }
                    else
                    {
                        if (c == '=')
                        {
                            throw new ExprParseException("Use '==' for comparison", _tokStart);
                        }
                        _text = c.ToString();
                    }
                    _tok = Tok.Op;
                    return;
                case '&':
                    if (_pos < _s.Length && _s[_pos] == '&')
                    {
                        _pos++;
                        _tok = Tok.Op; _text = "&&"; return;
                    }
                    throw new ExprParseException("Use '&&'", _tokStart);
                case '|':
                    if (_pos < _s.Length && _s[_pos] == '|')
                    {
                        _pos++;
                        _tok = Tok.Op; _text = "||"; return;
                    }
                    throw new ExprParseException("Use '||'", _tokStart);
                default:
                    throw new ExprParseException($"Unexpected character '{c}'", _tokStart);
            }
        }
    }
}
