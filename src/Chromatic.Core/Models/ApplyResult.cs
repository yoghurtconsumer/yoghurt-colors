namespace Chromatic.Core.Models;

public sealed record ApplyResult(bool Success, string Backend, string? ErrorCode,
    string? Explanation, long Revision)
{
    public static ApplyResult NotImplemented(string backend, long revision = 0) =>
        new(false, backend, "NotImplemented", "Native implementation is scheduled for a later stage.", revision);
}

public enum CompletionStatus { Accepted, StaleRevision, Invalid, Blocked, NotImplemented }
public sealed record CommandResult(CompletionStatus Status, long Revision, long Generation, string? Explanation);
