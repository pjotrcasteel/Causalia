namespace Causalia.FailureIntelligence;

/// <summary>
/// Identifies one semantic failure independently from seed, concrete schedule and replay token.
/// </summary>
public sealed record FailureSignature
{
    internal FailureSignature(string token, FailureKind kind, string exceptionType, string semanticKey)
    {
        Token = token;
        Kind = kind;
        ExceptionType = exceptionType;
        SemanticKey = semanticKey;
    }

    /// <summary>
    /// Gets the stable versioned failure-intelligence token.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the semantic failure classification represented by this signature.
    /// </summary>
    public FailureKind Kind { get; }

    /// <summary>
    /// Gets the fully-qualified exception type used by the signature.
    /// </summary>
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the stable semantic key that was hashed into the token.
    /// </summary>
    public string SemanticKey { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Token;
    }
}
