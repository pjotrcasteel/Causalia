namespace Causalia.Faults;

internal interface IInjectorScopedFaultPolicy<TContext, TEffect>
{
    IFaultPolicy<TContext, TEffect> CreateForInjector();
}
