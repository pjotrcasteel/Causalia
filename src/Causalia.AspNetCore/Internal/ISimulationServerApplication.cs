using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal interface ISimulationServerApplication
{
    Task ProcessAsync(IFeatureCollection features);
}
