namespace Limaj.Framework.Abstractions.Common;

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(Error error) => new(false, default, error);

    public static Result<T> Validation(
        IDictionary<string, string[]> errors,
        string code = "validation_failed",
        string message = "Validation failed.") =>
        Fail(new Error(code, message, ErrorType.Validation, new Dictionary<string, string[]>(errors)));

    public static Result<T> NotFound(string code = "not_found", string message = "Resource not found.") =>
        Fail(new Error(code, message, ErrorType.NotFound));

    public static Result<T> Conflict(string code = "conflict", string message = "Conflict.") =>
        Fail(new Error(code, message, ErrorType.Conflict));

    public static Result<T> Forbidden(string code = "forbidden", string message = "Forbidden.") =>
        Fail(new Error(code, message, ErrorType.Forbidden));

    public static Result<T> Unauthorized(string code = "unauthorized", string message = "Unauthorized.") =>
        Fail(new Error(code, message, ErrorType.Unauthorized));

    public static Result<T> Unexpected(string code = "unexpected", string message = "Unexpected error.") =>
        Fail(new Error(code, message, ErrorType.Unexpected));
}

public sealed class Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public Error? Error { get; }

    public static Result Ok() => new(true, null);
    public static Result Fail(Error error) => new(false, error);

    public static Result Validation(
        IDictionary<string, string[]> errors,
        string code = "validation_failed",
        string message = "Validation failed.") =>
        Fail(new Error(code, message, ErrorType.Validation, new Dictionary<string, string[]>(errors)));

    public static Result NotFound(string code = "not_found", string message = "Resource not found.") =>
        Fail(new Error(code, message, ErrorType.NotFound));

    public static Result Conflict(string code = "conflict", string message = "Conflict.") =>
        Fail(new Error(code, message, ErrorType.Conflict));

    public static Result Forbidden(string code = "forbidden", string message = "Forbidden.") =>
        Fail(new Error(code, message, ErrorType.Forbidden));

    public static Result Unauthorized(string code = "unauthorized", string message = "Unauthorized.") =>
        Fail(new Error(code, message, ErrorType.Unauthorized));

    public static Result Unexpected(string code = "unexpected", string message = "Unexpected error.") =>
        Fail(new Error(code, message, ErrorType.Unexpected));
}
