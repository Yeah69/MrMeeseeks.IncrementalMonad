using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MrMeeseeks.IncrementalMonad;

public class IncrementalMonad<T>
{
    private readonly T? _value;
    private readonly Diagnostic[] _diagnostics = [];
    
    public IncrementalMonad(T? value) => _value = value;
    
    public IncrementalMonad(IEnumerable<Diagnostic> diagnostics)
    {
        _value = default;
        _diagnostics = diagnostics.ToArray();
    }
    
    public IncrementalMonad(T? value, IEnumerable<Diagnostic> diagnostics)
    {
        _value = value;
        _diagnostics = diagnostics.ToArray();
    }
    
    public bool IsAborted { get; private init; }
    
    public IncrementalMonad<TNext> Bind<TNext>(Func<T?, Action<Diagnostic>, TNext> func)
    {
        var nextDiagnostics = new List<Diagnostic>();
        try
        {
            if (IsAborted)
                return new IncrementalMonad<TNext>(_diagnostics.ToArray());
            var nextValue = func(_value, ProcessDiagnostic);
            return new IncrementalMonad<TNext>(nextValue, _diagnostics.Concat(nextDiagnostics).ToArray());
        }
        catch (Exception e)
        {
            ProcessDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "MONAD0",
                    "Error",
                    $"Unexpected error: {e.Message}",
                    "IncrementalMonad",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
            
            return new IncrementalMonad<TNext>(_diagnostics.Concat(nextDiagnostics).ToArray())
            {
                IsAborted = true // An uncaught exception automatically aborts the monad
            };
        }
        
        void ProcessDiagnostic(Diagnostic diagnostic) => nextDiagnostics.Add(diagnostic);
    }
    
    public void Sink(SourceProductionContext context, Action<T?> sinkingLegitValue)
    {
        foreach (var diagnostic in _diagnostics)
            context.ReportDiagnostic(diagnostic);
        try
        {
            if (!IsAborted)
                sinkingLegitValue(_value);
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "MONAD1",
                    "Error",
                    $"Unexpected error during sinking: {e.Message}",
                    "IncrementalMonad",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
        }
    }
    
    public IncrementalMonad<T> Abort() => new(_value, _diagnostics) { IsAborted = true };

    public static IncrementalMonad<ImmutableArray<T?>> Collect(IEnumerable<IncrementalMonad<T>> monads)
    {
        var diagnostics = new List<Diagnostic>();
        var values = new List<T?>();
        foreach (var monad in monads)
        {
            diagnostics.AddRange(monad._diagnostics);
            if (!monad.IsAborted)
                values.Add(monad._value);
        }
        return new IncrementalMonad<ImmutableArray<T?>>(values.ToImmutableArray(), diagnostics);
    }

    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    
    public override bool Equals(object? obj) => obj is IncrementalMonad<T> other && Equals(other);
}