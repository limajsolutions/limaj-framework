using System.Net;
using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-005 security fixes, on by default in 2.1.0: no ex.Source on the 500 body, the generic
/// message for a 5xx Result.Unexpected outside Development (with the temporary opt-out), and
/// Validation errors kept when an explicit status is set.
/// </summary>
public class SecurityFixesTests
{
    [Fact]
    public async Task UnhandledException_OutsideDevelopment_BodyCarriesNeitherSourceNorMessage_InTheStaticFacade()
    {
        var httpResult = await RequestRunner.RunAsync(
            () => throw new InvalidOperationException("SELECT * FROM users -- secret"),
            NullLogger.Instance,
            "Op");

        await AssertNoSourceAndNoMessageAsync(httpResult);
    }

    [Fact]
    public async Task UnhandledException_OutsideDevelopment_BodyCarriesNeitherSourceNorMessage_InTheResponder()
    {
        var options = new LimajHttpErrorOptions { Format = LimajProblemDetailsFormat.V3 };
        var responder = new HttpResultResponder(
            new DefaultErrorHttpMapper(options, () => false),
            new BuiltInExceptionToErrorMapper(),
            NullLogger.Instance,
            options,
            hostMapper: null,
            () => false);

        var httpResult = await responder.RunAsync(() => throw new InvalidOperationException("SELECT * FROM users -- secret"), "Op");

        var response = await AssertNoSourceAndNoMessageAsync(httpResult);
        Assert.Equal("unexpected_error", response.GetString("code"));
    }

    [Theory]
    [InlineData(LimajProblemDetailsFormat.V2)]
    [InlineData(LimajProblemDetailsFormat.V3)]
    public async Task UnexpectedResult_OutsideDevelopment_SendsTheGenericMessage(LimajProblemDetailsFormat format)
    {
        var mapper = new DefaultErrorHttpMapper(new LimajHttpErrorOptions { Format = format }, () => false);

        var response = await HttpResultExecutor.ExecuteAsync(mapper.Map(new Error("db_failure", "Login failed for user 'sa'.", ErrorType.Unexpected)));

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Contains(DefaultErrorHttpMapper.GenericUnexpectedMessage, response.RawBody);
        Assert.DoesNotContain("Login failed", response.RawBody);
    }

    [Fact]
    public async Task UnexpectedResult_WithTheTemporaryOptOut_SendsItsOwnMessage()
    {
#pragma warning disable CS0618 // The temporary DA-005 opt-out is deprecated on purpose.
        var options = new LimajHttpErrorOptions { ExposeUnexpectedResultMessage = true };
#pragma warning restore CS0618
        var mapper = new DefaultErrorHttpMapper(options, () => false);

        var response = await HttpResultExecutor.ExecuteAsync(mapper.Map(new Error("db_failure", "Login failed.", ErrorType.Unexpected)));

        Assert.Equal("Login failed.", response.GetString("title"));
    }

    [Fact]
    public void ExposeUnexpectedResultMessage_IsObsolete()
    {
        var property = typeof(LimajHttpErrorOptions).GetProperty("ExposeUnexpectedResultMessage")!;

        Assert.NotNull(Attribute.GetCustomAttribute(property, typeof(ObsoleteAttribute)));
    }

    [Fact]
    public async Task UnexpectedResult_WithAnExplicitClientStatus_KeepsItsMessage()
    {
        // The generic message protects 5xx responses; a host that deliberately answers a 4xx
        // through the deprecated escape hatch (Type left at its Unexpected default) keeps it.
#pragma warning disable CS0618
        var error = new Error("plan_limit_exceeded", "Plan limit exceeded.", HttpStatusCode: HttpStatusCode.PaymentRequired);
#pragma warning restore CS0618

        var response = await HttpResultExecutor.ExecuteAsync(Result.Fail(error).ToHttpResult(Results.NoContent));

        Assert.Equal("Plan limit exceeded.", response.GetString("title"));
    }

    [Theory]
    [InlineData(LimajProblemDetailsFormat.V2)]
    [InlineData(LimajProblemDetailsFormat.V3)]
    public async Task ValidationWithExplicitStatus_KeepsItsErrors(LimajProblemDetailsFormat format)
    {
#pragma warning disable CS0618
        var error = new Error(
            "attachment_too_large",
            "The attachment is too large.",
            ErrorType.Validation,
            new Dictionary<string, string[]> { ["attachment"] = ["must be at most 10 MB"] },
            HttpStatusCode.RequestEntityTooLarge);
#pragma warning restore CS0618
        var mapper = new DefaultErrorHttpMapper(new LimajHttpErrorOptions { Format = format }, () => false);

        var response = await HttpResultExecutor.ExecuteAsync(mapper.Map(error));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, response.StatusCode);
        Assert.Equal("must be at most 10 MB", response.GetProperty("errors").GetProperty("attachment")[0].GetString());
    }

    private static async Task<ExecutedHttpResult> AssertNoSourceAndNoMessageAsync(IResult httpResult)
    {
        var response = await HttpResultExecutor.ExecuteAsync(httpResult);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret", response.RawBody);
        Assert.DoesNotContain(typeof(SecurityFixesTests).Assembly.GetName().Name!, response.RawBody);
        Assert.Contains(DefaultErrorHttpMapper.GenericUnexpectedMessage, response.RawBody);
        return response;
    }
}
