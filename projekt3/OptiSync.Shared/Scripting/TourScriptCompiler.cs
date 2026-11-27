using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace OptiSync.Shared.Scripting;

public sealed class TourScriptCompilationException : Exception
{
    public TourScriptCompilationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public static class TourScriptCompiler
{
    public const string DefaultScript =
        """
        (tour, rng) =>
        {
            int i = rng.Next(tour.Length);
            int j = rng.Next(tour.Length);
            (tour[i], tour[j]) = (tour[j], tour[i]);
        }
        """;

    public static async Task<Action<int[], Random>> CompileAsync(string code, CancellationToken ct = default)
    {
        var options = ScriptOptions.Default
            .AddReferences(typeof(object).Assembly, typeof(Enumerable).Assembly)
            .AddImports("System", "System.Linq", "System.Collections.Generic");

        Script<Action<int[], Random>> script = CSharpScript.Create<Action<int[], Random>>(code, options);

        var errors = script.Compile(ct)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        if (errors.Count > 0)
        {
            throw new TourScriptCompilationException(errors);
        }

        var state = await script.RunAsync(cancellationToken: ct).ConfigureAwait(false);
        return state.ReturnValue
            ?? throw new TourScriptCompilationException(["Skrypt musi kończyć się wyrażeniem typu Action<int[], Random>, np. (tour, rng) => { ... }."]);
    }
}
