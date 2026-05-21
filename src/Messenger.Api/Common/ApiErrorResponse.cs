namespace Messenger.Api.Common;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    int StatusCode,
    DateTime TimestampUtc
);