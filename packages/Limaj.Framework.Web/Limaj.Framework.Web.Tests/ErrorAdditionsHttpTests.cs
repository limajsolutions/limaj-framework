using Limaj.Framework.Core;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-007 (RetryAfter -> Retry-After header, body field only in V3) and DA-008 (Details outside
/// Validation only on opt-in, never on a 5xx).
/// </summary>
public class ErrorAdditionsHttpTests
{
    [Theory]
    [InlineData(LimajProblemDetailsFormat.V2)]
    [InlineData(LimajProblemDetailsFormat.V3)]
    public async Task RetryAfter_EmitsTheHeaderInWholeSecondsRoundedUp(LimajProblemDetailsFormat format)
    {
        var error = new Error("rate_limited", "Slow down.", ErrorType.TooManyRequests) { RetryAfter = TimeSpan.FromMilliseconds(1200) };

        var response = await HttpResultExecutor.ExecuteAsync(CreateMapper(format).Map(error));

        Assert.Equal(StatusCodes.Status429TooManyRequests, response.StatusCode);
        Assert.Equal("2", response.Headers.RetryAfter.ToString());
        Assert.Equal(format == LimajProblemDetailsFormat.V3, response.HasProperty("retryAfter"));
        if (format == LimajProblemDetailsFormat.V3)
        {
            Assert.Equal(2, response.GetProperty("retryAfter").GetInt64());
        }
    }

    [Fact]
    public void RetryAfter_WrapsTheResultButKeepsItsStatusCodeReachable()
    {
        var error = new Error("rate_limited", "Slow down.", ErrorType.TooManyRequests) { RetryAfter = TimeSpan.FromSeconds(30) };

        var httpResult = Result.Fail(error).ToHttpResult(Results.NoContent);

        var retryAfterResult = Assert.IsType<RetryAfterHttpResult>(httpResult);
        Assert.Equal(30, retryAfterResult.RetryAfterSeconds);
        Assert.Equal(StatusCodes.Status429TooManyRequests, retryAfterResult.StatusCode);
        Assert.IsType<ProblemHttpResult>(retryAfterResult.InnerResult);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RetryAfter_NotSetOrNotPositive_EmitsNoHeaderAndKeepsTheConcreteType(int? retryAfterSeconds)
    {
        var error = new Error("rate_limited", "Slow down.", ErrorType.TooManyRequests)
        {
            RetryAfter = retryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null
        };

        var httpResult = CreateMapper(LimajProblemDetailsFormat.V3).Map(error);

        Assert.IsType<ProblemHttpResult>(httpResult);
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.False(response.Headers.ContainsKey("Retry-After"));
        Assert.False(response.HasProperty("retryAfter"));
    }

    [Theory]
    [InlineData(ErrorType.NotFound, LimajProblemDetailsFormat.V2)]
    [InlineData(ErrorType.NotFound, LimajProblemDetailsFormat.V3)]
    [InlineData(ErrorType.Conflict, LimajProblemDetailsFormat.V2)]
    [InlineData(ErrorType.Forbidden, LimajProblemDetailsFormat.V3)]
    public async Task DetailsOutsideValidation_AreDroppedByDefault(ErrorType errorType, LimajProblemDetailsFormat format)
    {
        var response = await HttpResultExecutor.ExecuteAsync(CreateMapper(format).Map(ErrorWithDetails(errorType)));

        Assert.False(response.HasProperty("details"));
        Assert.DoesNotContain("internal-id-42", response.RawBody);
    }

    [Theory]
    [InlineData(ErrorType.NotFound, LimajProblemDetailsFormat.V2)]
    [InlineData(ErrorType.NotFound, LimajProblemDetailsFormat.V3)]
    [InlineData(ErrorType.Conflict, LimajProblemDetailsFormat.V2)]
    [InlineData(ErrorType.Forbidden, LimajProblemDetailsFormat.V3)]
    public async Task DetailsOutsideValidation_AreSentWithTheOptIn(ErrorType errorType, LimajProblemDetailsFormat format)
    {
        var response = await HttpResultExecutor.ExecuteAsync(CreateMapper(format, includeDetails: true).Map(ErrorWithDetails(errorType)));

        Assert.Equal("internal-id-42", response.GetProperty("details").GetProperty("reference")[0].GetString());
    }

    [Fact]
    public async Task DetailsOutsideValidation_AreNeverSentOnA5xx_EvenWithTheOptIn()
    {
        var mapper = CreateMapper(LimajProblemDetailsFormat.V3, includeDetails: true);
#pragma warning disable CS0618 // A deprecated explicit 5xx status must not leak Details either.
        var explicitServerError = ErrorWithDetails(ErrorType.TooManyRequests) with { HttpStatusCode = System.Net.HttpStatusCode.ServiceUnavailable };
#pragma warning restore CS0618

        var unexpected = await HttpResultExecutor.ExecuteAsync(mapper.Map(ErrorWithDetails(ErrorType.Unexpected)));
        var serviceUnavailable = await HttpResultExecutor.ExecuteAsync(mapper.Map(explicitServerError));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, serviceUnavailable.StatusCode);
        Assert.DoesNotContain("internal-id-42", unexpected.RawBody);
        Assert.DoesNotContain("internal-id-42", serviceUnavailable.RawBody);
    }

    [Fact]
    public async Task ValidationErrors_AreAlwaysSent_AndNotDuplicatedAsDetails()
    {
        var error = new Error("validation_failed", "Invalid.", ErrorType.Validation, new Dictionary<string, string[]> { ["field"] = ["required"] });

        var response = await HttpResultExecutor.ExecuteAsync(CreateMapper(LimajProblemDetailsFormat.V3, includeDetails: true).Map(error));

        Assert.True(response.HasProperty("errors"));
        Assert.False(response.HasProperty("details"));
    }

    private static DefaultErrorHttpMapper CreateMapper(LimajProblemDetailsFormat format, bool includeDetails = false) =>
        new(new LimajHttpErrorOptions { Format = format, IncludeDetailsOutsideValidation = includeDetails }, () => false);

    private static Error ErrorWithDetails(ErrorType errorType) =>
        new("code", "message", errorType, new Dictionary<string, string[]> { ["reference"] = ["internal-id-42"] });
}
