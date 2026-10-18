using System.Buffers;
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
/// <remarks>
/// The tree has one node class per operator and per call kind, so evaluating a node is one virtual call
/// with no dispatch on operator text. Function arguments live in a stack buffer (a pooled one for long
/// argument lists), so evaluation allocates nothing.
/// </remarks>
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

    /// <summary>
    /// Whether the tree calls only pure deterministic functions. Scope dependencies must also be
    /// checked by the caller: an identifier may itself read a random attribute formula.
    /// Conservative across both branches of a conditional; this never evaluates the expression.
    /// </summary>
    public bool IsPreviewSafe => _root.IsPreviewSafe;

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
        public virtual bool IsPreviewSafe => true;
        public abstract double Eval(IExprScope scope);
        public abstract void Collect(List<string> identifiers, List<string> flags);
    }

    private sealed class ConstNode : Node
    {
        public readonly double Value;
        public ConstNode(double v) => Value = v;
        public override double Eval(IExprScope scope) => Value;
        public override void Collect(List<string> identifiers, List<string> flags) { }
    }

    private sealed class StringNode : Node
    {
        public readonly string Value;
        public StringNode(string v) => Value = v;
        public override double Eval(IExprScope scope) => throw new ExprEvalException($"String \"{Value}\" used where a number was expected.");
        public override void Collect(List<string> identifiers, List<string> flags) { }
    }

    private sealed class VarNode : Node
    {
        private readonly string _name;
        public VarNode(string name) => _name = name;

        public override double Eval(IExprScope scope) => scope.TryResolve(_name, out var v) ? v : throw Unknown(_name);

        private static ExprEvalException Unknown(string name) => new($"Unknown identifier '{name}'.");

        public override void Collect(List<string> identifiers, List<string> flags) => identifiers.Add(_name);
    }

    private abstract class UnaryNode : Node
    {
        public override bool IsPreviewSafe => A.IsPreviewSafe;
        protected readonly Node A;
        protected UnaryNode(Node a) => A = a;
        public sealed override void Collect(List<string> identifiers, List<string> flags) => A.Collect(identifiers, flags);
    }

    private sealed class NegateNode : UnaryNode
    {
        public NegateNode(Node a) : base(a) { }
        public override double Eval(IExprScope scope) => -A.Eval(scope);
    }

    private sealed class NotNode : UnaryNode
    {
        public NotNode(Node a) : base(a) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) == 0 ? 1 : 0;
    }

    /// <summary>Operands evaluate left then right (rand() draws keep their order).</summary>
    private abstract class BinaryNode : Node
    {
        public override bool IsPreviewSafe => A.IsPreviewSafe && B.IsPreviewSafe;
        protected readonly Node A;
        protected readonly Node B;
        protected BinaryNode(Node a, Node b) { A = a; B = b; }

        public sealed override void Collect(List<string> identifiers, List<string> flags)
        {
            A.Collect(identifiers, flags);
            B.Collect(identifiers, flags);
        }

        public static Node Make(string op, Node a, Node b) => op switch
        {
            "+" => new AddNode(a, b),
            "-" => new SubtractNode(a, b),
            "*" => new MultiplyNode(a, b),
            "/" => new DivideNode(a, b),
            "%" => new ModuloNode(a, b),
            "^" => new PowerNode(a, b),
            "<" => new LessNode(a, b),
            "<=" => new LessEqualNode(a, b),
            ">" => new GreaterNode(a, b),
            ">=" => new GreaterEqualNode(a, b),
            "==" => new EqualNode(a, b),
            "!=" => new NotEqualNode(a, b),
            "&&" => new AndNode(a, b),
            "||" => new OrNode(a, b),
            _ => throw new ExprEvalException($"Unknown operator {op}.")
        };
    }

    private sealed class AddNode : BinaryNode
    {
        public AddNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) + B.Eval(scope);
    }

    private sealed class SubtractNode : BinaryNode
    {
        public SubtractNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) - B.Eval(scope);
    }

    private sealed class MultiplyNode : BinaryNode
    {
        public MultiplyNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) * B.Eval(scope);
    }

    /// <summary>Division by zero yields zero.</summary>
    private sealed class DivideNode : BinaryNode
    {
        public DivideNode(Node a, Node b) : base(a, b) { }

        public override double Eval(IExprScope scope)
        {
            var x = A.Eval(scope);
            var y = B.Eval(scope);
            return y == 0 ? 0 : x / y;
        }
    }

    private sealed class ModuloNode : BinaryNode
    {
        public ModuloNode(Node a, Node b) : base(a, b) { }

        public override double Eval(IExprScope scope)
        {
            var x = A.Eval(scope);
            var y = B.Eval(scope);
            return y == 0 ? 0 : x % y;
        }
    }

    private sealed class PowerNode : BinaryNode
    {
        public PowerNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => Math.Pow(A.Eval(scope), B.Eval(scope));
    }

    private sealed class LessNode : BinaryNode
    {
        public LessNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) < B.Eval(scope) ? 1 : 0;
    }

    private sealed class LessEqualNode : BinaryNode
    {
        public LessEqualNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) <= B.Eval(scope) ? 1 : 0;
    }

    private sealed class GreaterNode : BinaryNode
    {
        public GreaterNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) > B.Eval(scope) ? 1 : 0;
    }

    private sealed class GreaterEqualNode : BinaryNode
    {
        public GreaterEqualNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) >= B.Eval(scope) ? 1 : 0;
    }

    private sealed class EqualNode : BinaryNode
    {
        public EqualNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => Math.Abs(A.Eval(scope) - B.Eval(scope)) < 1e-9 ? 1 : 0;
    }

    private sealed class NotEqualNode : BinaryNode
    {
        public NotEqualNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => Math.Abs(A.Eval(scope) - B.Eval(scope)) >= 1e-9 ? 1 : 0;
    }

    private sealed class AndNode : BinaryNode
    {
        public AndNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) != 0 && B.Eval(scope) != 0 ? 1 : 0;
    }

    private sealed class OrNode : BinaryNode
    {
        public OrNode(Node a, Node b) : base(a, b) { }
        public override double Eval(IExprScope scope) => A.Eval(scope) != 0 || B.Eval(scope) != 0 ? 1 : 0;
    }

    private sealed class TernaryNode : Node
    {
        public override bool IsPreviewSafe => _c.IsPreviewSafe && _a.IsPreviewSafe && _b.IsPreviewSafe;
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

    private sealed class HasNode : Node
    {
        private readonly string _flag;
        public HasNode(string flag) => _flag = flag;
        public override double Eval(IExprScope scope) => scope.HasFlag(_flag) ? 1 : 0;
        public override void Collect(List<string> identifiers, List<string> flags) => flags.Add(_flag);
    }

    private sealed class RandNode : Node
    {
        public override bool IsPreviewSafe => false;
        public override double Eval(IExprScope scope) => scope.NextRandom();
        public override void Collect(List<string> identifiers, List<string> flags) { }
    }

    /// <summary>
    /// <c>randi(lo, hi)</c>: a whole number from lo to hi inclusive, drawn from the scope's random stream
    /// like <c>rand()</c>. The bounds round inward to whole numbers and may come in either order; when no
    /// whole number lies between them the result is the lower bound rounded up.
    /// </summary>
    private sealed class RandIntNode : Node
    {
        public override bool IsPreviewSafe => false;
        private readonly Node _lo, _hi;
        public RandIntNode(Node lo, Node hi) { _lo = lo; _hi = hi; }

        public override double Eval(IExprScope scope)
        {
            var a = _lo.Eval(scope);
            var b = _hi.Eval(scope);
            var low = Math.Ceiling(Math.Min(a, b));
            var high = Math.Floor(Math.Max(a, b));
            if (high <= low)
            {
                return low;
            }
            var roll = Math.Floor(scope.NextRandom() * (high - low + 1));
            return Math.Min(high, low + roll);
        }

        public override void Collect(List<string> identifiers, List<string> flags)
        {
            _lo.Collect(identifiers, flags);
            _hi.Collect(identifiers, flags);
        }
    }

    /// <summary><c>curve("name", t)</c>: a named curve from <see cref="Curves"/> at t; a curve unregistered at evaluation is linear.</summary>
    private sealed class CurveNode : Node
    {
        public override bool IsPreviewSafe => Curves.IsPreviewSafe(_curve) && _t.IsPreviewSafe;
        private readonly string _curve;
        private readonly Node _t;
        public CurveNode(string curve, Node t) { _curve = curve; _t = t; }

        public override double Eval(IExprScope scope) => Curves.Evaluate(_curve, _t.Eval(scope));

        public override void Collect(List<string> identifiers, List<string> flags) => _t.Collect(identifiers, flags);
    }

    /// <summary>A registered function. Arguments evaluate in order into a stack buffer the function reads as a span.</summary>
    private sealed class CallNode : Node
    {
        public const int StackArgs = 8;

        private readonly Node[] _args;
        private readonly ExprFunctions.Body _impl;
        private readonly bool _previewSafe;
        public override bool IsPreviewSafe => _previewSafe && _args.All(a => a.IsPreviewSafe);

        private CallNode(Node[] args, ExprFunctions.Body impl, bool previewSafe)
        {
            _args = args;
            _impl = impl;
            _previewSafe = previewSafe;
        }

        /// <summary>Validates a call while parsing: built-in forms first, then the function registry.</summary>
        public static Node Make(string name, Node[] args, int position)
        {
            switch (name)
            {
                case "has":
                    if (args.Length != 1 || args[0] is not StringNode flag)
                    {
                        throw new ExprParseException("has() takes exactly one quoted flag name", position);
                    }
                    return new HasNode(flag.Value);
                case "rand":
                    if (args.Length != 0)
                    {
                        throw new ExprParseException("rand() takes no arguments", position);
                    }
                    return new RandNode();
                case "randi":
                    if (args.Length != 2)
                    {
                        throw new ExprParseException("randi() takes a low and a high bound", position);
                    }
                    return new RandIntNode(args[0], args[1]);
                case "curve":
                    if (args.Length != 2 || args[0] is not StringNode curve)
                    {
                        throw new ExprParseException("curve() takes a quoted curve name and a value", position);
                    }
                    if (!Curves.Has(curve.Value))
                    {
                        throw new ExprParseException($"Unknown curve '{curve.Value}'", position);
                    }
                    return new CurveNode(curve.Value, args[1]);
                case "if":
                    if (args.Length != 3)
                    {
                        throw new ExprParseException("if() takes condition, then, else", position);
                    }
                    return new TernaryNode(args[0], args[1], args[2]);
                default:
                    if (!ExprFunctions.TryGet(name, out var fn))
                    {
                        throw new ExprParseException($"Unknown function '{name}'", position);
                    }
                    if (args.Length < fn!.MinArgs || args.Length > fn.MaxArgs)
                    {
                        throw new ExprParseException($"{name}() expects {fn.MinArgs}..{fn.MaxArgs} arguments, got {args.Length}", position);
                    }
                    return new CallNode(args, fn.Impl, fn.PreviewSafe);
            }
        }

        public override double Eval(IExprScope scope)
        {
            var args = _args;
            if (args.Length > StackArgs)
            {
                return EvalPooled(scope);
            }
            Span<double> values = stackalloc double[StackArgs];
            values = values[..args.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = args[i].Eval(scope);
            }
            return _impl(values);
        }

        private double EvalPooled(IExprScope scope)
        {
            var args = _args;
            var rented = ArrayPool<double>.Shared.Rent(args.Length);
            try
            {
                var values = rented.AsSpan(0, args.Length);
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = args[i].Eval(scope);
                }
                return _impl(values);
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }

        public override void Collect(List<string> identifiers, List<string> flags)
        {
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
            return n;
        }

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
                n = new OrNode(n, ParseAnd());
            }
            return n;
        }

        private Node ParseAnd()
        {
            var n = ParseCompare();
            while (_tok == Tok.Op && _text == "&&")
            {
                Advance();
                n = new AndNode(n, ParseCompare());
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
                n = BinaryNode.Make(op, n, ParseAdd());
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
                n = BinaryNode.Make(op, n, ParseMul());
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
                n = BinaryNode.Make(op, n, ParseUnary());
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
                if (op == '-')
                {
                    return operand is ConstNode c ? new ConstNode(-c.Value) : new NegateNode(operand);
                }
                return new NotNode(operand);
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
                return new PowerNode(n, exponent);
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
                        return CallNode.Make(name, args.ToArray(), start);
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
                case '+': _tok = Tok.Op; _text = "+"; return;
                case '-': _tok = Tok.Op; _text = "-"; return;
                case '*': _tok = Tok.Op; _text = "*"; return;
                case '/': _tok = Tok.Op; _text = "/"; return;
                case '%': _tok = Tok.Op; _text = "%"; return;
                case '^': _tok = Tok.Op; _text = "^"; return;
                case '<': case '>': case '=': case '!':
                    if (_pos < _s.Length && _s[_pos] == '=')
                    {
                        _pos++;
                        _text = c switch { '<' => "<=", '>' => ">=", '=' => "==", _ => "!=" };
                    }
                    else
                    {
                        if (c == '=')
                        {
                            throw new ExprParseException("Use '==' for comparison", _tokStart);
                        }
                        _text = c switch { '<' => "<", '>' => ">", _ => "!" };
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
