using Limaj.Framework.Abstractions.Common;
using Xunit;

namespace Limaj.Framework.Abstractions.Tests;

public class ResultOfTTests
{
    [Fact]
    public void Ok_IsSuccessWithValueAndNoError()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_IsFailureWithErrorAndDefaultValue()
    {
        var error = new Error("custom_code", "custom message", ErrorType.Conflict);

        var result = Result<int>.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Validation_ProducesValidationErrorWithDetailsAndDefaults()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["required"] };

        var result = Result<int>.Validation(errors);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("validation_failed", result.Error.Code);
        Assert.Equal("Validation failed.", result.Error.Message);
        Assert.Equal(errors["field"], result.Error.Details!["field"]);
    }

    [Fact]
    public void Validation_AllowsCustomCodeAndMessage()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["required"] };

        var result = Result<int>.Validation(errors, "custom_code", "custom message");

        Assert.Equal("custom_code", result.Error!.Code);
        Assert.Equal("custom message", result.Error.Message);
    }

    [Fact]
    public void NotFound_DefaultsToNotFoundErrorType()
    {
        var result = Result<int>.NotFound();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("Resource not found.", result.Error.Message);
    }

    [Fact]
    public void NotFound_AllowsCustomCodeAndMessage()
    {
        var result = Result<int>.NotFound("user_not_found", "User was not found.");

        Assert.Equal("user_not_found", result.Error!.Code);
        Assert.Equal("User was not found.", result.Error.Message);
    }

    [Fact]
    public void Conflict_DefaultsToConflictErrorType()
    {
        var result = Result<int>.Conflict();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("conflict", result.Error.Code);
    }

    [Fact]
    public void Forbidden_DefaultsToForbiddenErrorType()
    {
        var result = Result<int>.Forbidden();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public void Unauthorized_DefaultsToUnauthorizedErrorType()
    {
        var result = Result<int>.Unauthorized();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("unauthorized", result.Error.Code);
    }

    [Fact]
    public void Unexpected_DefaultsToUnexpectedErrorType()
    {
        var result = Result<int>.Unexpected();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
        Assert.Equal("unexpected", result.Error.Code);
    }
}

public class ResultTests
{
    [Fact]
    public void Ok_IsSuccessWithNoError()
    {
        var result = Result.Ok();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_IsFailureWithError()
    {
        var error = new Error("custom_code", "custom message", ErrorType.Conflict);

        var result = Result.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Validation_ProducesValidationErrorWithDetailsAndDefaults()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = ["required"] };

        var result = Result.Validation(errors);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("validation_failed", result.Error.Code);
        Assert.Equal(errors["field"], result.Error.Details!["field"]);
    }

    [Fact]
    public void NotFound_DefaultsToNotFoundErrorType()
    {
        var result = Result.NotFound();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public void Conflict_DefaultsToConflictErrorType()
    {
        var result = Result.Conflict();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public void Forbidden_DefaultsToForbiddenErrorType()
    {
        var result = Result.Forbidden();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public void Unauthorized_DefaultsToUnauthorizedErrorType()
    {
        var result = Result.Unauthorized();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact]
    public void Unexpected_DefaultsToUnexpectedErrorType()
    {
        var result = Result.Unexpected();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }
}
