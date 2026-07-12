using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.Support;

/// <summary>Transforms one localization bundle to another and can optionally report diagnostics.</summary>
public abstract class UnaryLocBundleProcessor
{
    public abstract LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null);
}
