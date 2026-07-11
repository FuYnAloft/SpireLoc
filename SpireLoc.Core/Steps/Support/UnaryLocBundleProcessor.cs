using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.Support;

/// <summary>A pure transformation from one localization bundle to another.</summary>
public abstract class UnaryLocBundleProcessor
{
    public abstract LocBundle Process(LocBundle bundle);
}
