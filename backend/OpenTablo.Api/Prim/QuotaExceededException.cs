namespace OpenTablo.Api.Prim;

public sealed class QuotaExceededException(string message) : Exception(message);
