namespace OpenTablo.Api.Data;

/// <summary>Last successful refresh of a referential, used to decide when to download again.</summary>
public class ReferentialImport
{
    public required string Name { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
