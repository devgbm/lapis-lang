using System.Collections.Generic;
using System.Linq;

namespace LapisLang.Core;

public class DiagnosticsBag
{
    private List<Diagnostic> _dianostics = new();

    public bool HasErrors { get => _dianostics.Any(); }

    public IEnumerable<Diagnostic> Errors => _dianostics;

    public void Report(string v, SourceSpan? span = null)
    {
        _dianostics.Add(new Diagnostic(v, span));
    }

    public void Dump(DiagnosticsBag bag)
    {
        bag._dianostics.AddRange(_dianostics);
    }

    public string GetMessages()
    {
        return string.Join("\n\n", Errors.Select(e => e.GenerateMessage()));
    }
}
