using System.Text.Json;

namespace AxiomOps.Services;

/// <summary>
/// Standard envelope returned by every Axiom Administrator Core endpoint.
/// </summary>
public class AxiomResponse<T>
{
    public bool Success { get; set; }

    public string? CustomMessage { get; set; }

    /// <summary>Payload of the operation; shape depends on the endpoint.</summary>
    public T? DataObject { get; set; }

    /// <summary>Raw auxiliary result sets occasionally returned by the API.</summary>
    public JsonElement? ResultSets { get; set; }
}

/// <summary>Envelope for endpoints whose payload is untyped or empty.</summary>
public sealed class AxiomResponse : AxiomResponse<JsonElement?>;
