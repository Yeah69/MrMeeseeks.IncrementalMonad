using Microsoft.CodeAnalysis;

namespace MrMeeseeks.IncrementalMonad;

public class IncrementalMonad<T>
{
    private readonly T? _value;
    private readonly Diagnostic[] _diagnostics = [];
    
    public IncrementalMonad(T value) => _value = value;
    
    public IncrementalMonad(params Diagnostic[] diagnostics)
    {
        _diagnostics = diagnostics;
    }
    
    public IncrementalMonad(T value, params Diagnostic[] diagnostics)
    {
        _diagnostics = diagnostics;
        if (HasErrors)
            return;
        _value = value;
    }
    
    public IncrementalMonad<TNext> Bind<TNext>(Func<T, Action<Diagnostic>, TNext> func)
    {
        var nextDiagnostics = new List<Diagnostic>();
        try
        {
            if (_value is null)
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
                    "ResXToViewModelGenerator",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
            
            return new IncrementalMonad<TNext>(_diagnostics.Concat(nextDiagnostics).ToArray());
        }
        
        void ProcessDiagnostic(Diagnostic diagnostic) => nextDiagnostics.Add(diagnostic);
    }
    
    public void Sink(SourceProductionContext context, Action<T> sinkingLegitValue)
    {
        foreach (var diagnostic in _diagnostics)
            context.ReportDiagnostic(diagnostic);
        if (_value is not null && !HasErrors)
            sinkingLegitValue(_value);
    }
    
    private bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}