namespace Causalia.AspNetCore.Networking.Faults;

/// <summary>
/// Delivers additional copies of one logical HTTP request to the destination service.
/// </summary>
public sealed class DuplicateHttpRequestFault : HttpNetworkFault
{
    internal DuplicateHttpRequestFault(int additionalCopies)
    {
        AdditionalCopies = additionalCopies;
    }

    /// <summary>
    /// Gets the number of additional deliveries to create.
    /// </summary>
    public int AdditionalCopies { get; }
}
