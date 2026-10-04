using Limaj.Framework.Abstractions.Common;
using Limaj.Framework.Web.Http;
using Limaj.Framework.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Limaj.Framework.Web.Tests;

/// <summary>
/// DA-003 contract suite: every ErrorType x format, executed on a real HttpContext with and
/// without AddProblemDetails. Asserts status, Content-Type, the body fields of each contract,
/// traceId/customization when IProblemDetailsService is registered, and that the static facade
/// is the V2 mapper.
/// </summary>
public class ProblemDetailsContractTests
{
    private const string ProblemJson = "application/problem+json";
    private const string PlainJson = "application/json; charset=utf-8";

    public static TheoryData<ErrorType, LimajProblemDetailsFormat, bool> Matrix()
    {
        var matrix = new TheoryData<ErrorType, LimajProblemDetailsFormat, bool>();
        foreach (var errorType in Enum.GetValues<ErrorType>())
        {
            foreach (var format in Enum.GetValues<LimajProblemDetailsFormat>())
            {
                matrix.Add(errorType, format, false);
                matrix.Add(errorType, format, true);
            }
        }

        return matrix;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Map_WritesTheSelectedFormatsContract(
        ErrorType errorType,
        LimajProblemDetailsFormat format,
        bool withProblemDetailsService)
    {
        var mapper = new DefaultErrorHttpMapper(new LimajHttpErrorOptions { Format = format }, () => false);

        var response = await HttpResultExecutor.ExecuteAsync(
            mapper.Map(BuildError(errorType)),
            services => RegisterProblemDetails(services, withProblemDetailsService));

        var expectedStatusCode = DefaultErrorHttpMapper.ResolveStatusCode(BuildError(errorType));
        var expectedClientMessage = errorType == ErrorType.Unexpected ? DefaultErrorHttpMapper.GenericUnexpectedMessage : "message";
        var writtenByProblemDetailsPipeline =
            format == LimajProblemDetailsFormat.V3 || errorType is not (ErrorType.NotFound or ErrorType.Conflict);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(writtenByProblemDetailsPipeline ? ProblemJson : PlainJson, response.ContentType);
        Assert.Equal(withProblemDetailsService && writtenByProblemDetailsPipeline, response.HasProperty("traceId"));
        Assert.Equal(withProblemDetailsService && writtenByProblemDetailsPipeline, response.HasProperty("customized"));

        if (format == LimajProblemDetailsFormat.V3)
        {
            Assert.Equal("code", response.GetString("code"));
            Assert.Equal(expectedClientMessage, response.GetString("detail"));
            Assert.Equal(Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(expectedStatusCode), response.GetString("title"));
        }
        else
        {
            Assert.False(response.HasProperty("code"));
            Assert.Equal(expectedClientMessage, response.GetString("title"));
            Assert.Equal(errorType == ErrorType.Validation ? null : "code", response.GetString("detail"));
        }

        Assert.Equal(errorType == ErrorType.Validation, response.HasProperty("errors"));
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Unexpected)]
    [InlineData(ErrorType.TooManyRequests)]
    public async Task StaticFacade_WritesTheSameResponseAsTheV2Mapper(ErrorType errorType)
    {
        var error = BuildError(errorType);
        var v2Mapper = new DefaultErrorHttpMapper(new LimajHttpErrorOptions { Format = LimajProblemDetailsFormat.V2 }, () => false);

        var facadeResult = Result.Fail(error).ToHttpResult(Results.NoContent);
        var mapperResult = v2Mapper.Map(error);

        Assert.Equal(mapperResult.GetType(), facadeResult.GetType());
        var facadeResponse = await HttpResultExecutor.ExecuteAsync(facadeResult);
        var mapperResponse = await HttpResultExecutor.ExecuteAsync(mapperResult);
        Assert.Equal(mapperResponse.StatusCode, facadeResponse.StatusCode);
        Assert.Equal(mapperResponse.ContentType, facadeResponse.ContentType);
        Assert.Equal(mapperResponse.RawBody, facadeResponse.RawBody);
    }

    [Fact]
    public void Options_DefaultFormat_IsV2()
    {
        Assert.Equal(LimajProblemDetailsFormat.V2, new LimajHttpErrorOptions().Format);
    }

    [Fact]
    public async Task V3_MapWithStatusCode_WritesTheCustomStatusInTheSameContract()
    {
        // The supported replacement for Error.HttpStatusCode (DA-010): a product mapper keyed
        // on Error.Code answering, e.g., 422 for a business rule violation. Until 3.0.0 adds
        // ErrorType.BusinessRule (DA-009) such an error has no type of its own, so Type stays
        // at its default and the mapper keys on the code.
        var mapper = new DefaultErrorHttpMapper(new LimajHttpErrorOptions { Format = LimajProblemDetailsFormat.V3 }, () => false);

        var response = await HttpResultExecutor.ExecuteAsync(
            mapper.MapWithStatusCode(new Error("order_already_shipped", "The order has already shipped."), StatusCodes.Status422UnprocessableEntity));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemJson, response.ContentType);
        Assert.Equal("order_already_shipped", response.GetString("code"));
        Assert.Equal("The order has already shipped.", response.GetString("detail"));
        Assert.Equal("Unprocessable Entity", response.GetString("title"));
    }

    private static void RegisterProblemDetails(IServiceCollection services, bool withProblemDetailsService)
    {
        if (withProblemDetailsService)
        {
            services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["customized"] = true);
        }
    }

    private static Error BuildError(ErrorType errorType) =>
        errorType == ErrorType.Validation
            ? new Error("code", "message", errorType, new Dictionary<string, string[]> { ["field"] = ["required"] })
            : new Error("code", "message", errorType);
}
