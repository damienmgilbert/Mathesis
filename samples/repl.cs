// A small read-eval-print loop for trying Mathesis by hand. Run: dotnet run samples/repl.cs
// Type "help" for the commands. Expressions use the linear notation of docs/design/05-syntax-trees-and-notation.md.
#:project ../src/Mathesis/Mathesis.csproj

using Mathesis;
using Mathesis.Calculus;
using Mathesis.Explanation;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var x = new Symbol("x");
var steps = true;
const string Help = """
    simplify <expr>        expand <expr>        factor <expr>        together <expr>        cancel <expr>
    diff <expr>            diff2 <expr> (second derivative)
    integrate <expr>       integrate <expr> from <a> to <b>
    limit <expr> at <point>                      (point may be oo or -oo)
    solve <equation or inequality>               solve <eq1> ; <eq2> in x,y
    taylor <expr> at <center> order <n>
    find <words>           (search the catalog)       show <entry id>
    steps on|off           (show the steps of each result)          quit
    """;
Console.WriteLine("Mathesis REPL. Type help for the commands, quit to leave.");

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line is null || line.Trim() is "quit" or "exit") break;
    line = line.Trim();
    if (line.Length == 0) continue;
    var space = line.IndexOf(' ', StringComparison.Ordinal);
    var (command, rest) = space < 0 ? (line, string.Empty) : (line[..space], line[(space + 1)..].Trim());
    try
    {
        switch (command)
        {
            case "help": Console.WriteLine(Help); break;
            case "steps": steps = rest != "off"; Console.WriteLine($"steps {(steps ? "on" : "off")}"); break;
            case "simplify": Show(Cas.Simplify(Expr.Parse(rest))); break;
            case "expand": Show(Cas.Expand(Expr.Parse(rest))); break;
            case "factor": Show(Cas.Factor(Expr.Parse(rest))); break;
            case "together": Show(Cas.Together(Expr.Parse(rest))); break;
            case "cancel": Show(Cas.Cancel(Expr.Parse(rest))); break;
            case "diff": Show(Cas.Differentiate(Expr.Parse(rest), x)); break;
            case "diff2": Show(Cas.Differentiate(Expr.Parse(rest), x, 2)); break;
            case "integrate" when rest.Contains(" from ", StringComparison.Ordinal):
            {
                var from = rest.IndexOf(" from ", StringComparison.Ordinal);
                var to = rest.IndexOf(" to ", StringComparison.Ordinal);
                Show(Cas.Integrate(Expr.Parse(rest[..from]), x, Expr.Parse(rest[(from + 6)..to]), Expr.Parse(rest[(to + 4)..])));
                break;
            }
            case "integrate": Show(Cas.Integrate(Expr.Parse(rest), x)); break;
            case "limit":
            {
                var at = rest.LastIndexOf(" at ", StringComparison.Ordinal);
                var outcome = Cas.Limit(Expr.Parse(rest[..at]), x, Expr.Parse(rest[(at + 4)..]));
                Console.WriteLine(outcome is Outcome<LimitResult>.Success { Value: var limit } ? limit.ToExpression() : outcome);
                if (steps && outcome is Outcome<LimitResult>.Success { Steps: { } limitSteps }) Console.WriteLine(limitSteps.Render());
                break;
            }
            case "solve" when rest.Contains(" in ", StringComparison.Ordinal):
            {
                var inIndex = rest.LastIndexOf(" in ", StringComparison.Ordinal);
                var equations = rest[..inIndex].Split(';').Select(e => Expr.Parse(e)).ToList();
                var variables = rest[(inIndex + 4)..].Split(',').Select(v => new Symbol(v.Trim())).ToList();
                ShowSet(Cas.Solve(equations, variables));
                break;
            }
            case "solve": ShowSet(Cas.Solve(Expr.Parse(rest), x)); break;
            case "taylor":
            {
                var at = rest.LastIndexOf(" at ", StringComparison.Ordinal);
                var order = rest.LastIndexOf(" order ", StringComparison.Ordinal);
                Show(Cas.Taylor(Expr.Parse(rest[..at]), x, Expr.Parse(rest[(at + 4)..order]), int.Parse(rest[(order + 7)..], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            }
            case "find":
                foreach (var entry in Cas.Find(rest).Take(10)) Console.WriteLine($"{entry.Id.Value}  {entry.Name}");
                break;
            case "show":
            {
                var entry = Cas.Get(rest);
                Console.WriteLine($"{entry.Id.Value}: {entry.Name} ({entry.Kind}, {entry.Level})\n  {entry.Statement}\n  {entry.Explain}");
                break;
            }
            default: Console.WriteLine($"Unknown command '{command}'. Type help."); break;
        }
    }
    catch (Exception e) when (e is ParseException or ArgumentException or KeyNotFoundException or FormatException or IndexOutOfRangeException)
    {
        Console.WriteLine($"Error: {e.Message}");
    }
}

void Show(Outcome<Expr> outcome)
{
    switch (outcome)
    {
        case Outcome<Expr>.Success { Value: var value, Steps: var derivation, Provisos: var provisos }:
            Console.WriteLine(value);
            if (provisos.Count > 0) Console.WriteLine($"  provided that {provisos}");
            if (steps) Console.WriteLine(derivation.Render());
            break;
        case Outcome<Expr>.Partial partial:
            Console.WriteLine($"{partial.Value}  (partial: {partial.Reason})");
            break;
        case Outcome<Expr>.Unevaluated unevaluated:
            Console.WriteLine($"unevaluated: {unevaluated.Reason}");
            break;
        default:
            Console.WriteLine(outcome);
            break;
    }
}

void ShowSet(Outcome<SolutionSet> outcome)
{
    if (outcome is Outcome<SolutionSet>.Success { Value: var set, Steps: var derivation, Provisos: var provisos })
    {
        Console.WriteLine(set);
        if (provisos.Count > 0) Console.WriteLine($"  provided that {provisos}");
        if (steps) Console.WriteLine(derivation.Render());
    }
    else
    {
        Console.WriteLine(outcome is Outcome<SolutionSet>.Unevaluated u ? $"unevaluated: {u.Reason}" : outcome);
    }
}
