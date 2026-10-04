using Limaj.Framework.Abstractions.Common;
using Microsoft.AspNetCore.Http;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// Static facade over <see cref="DefaultErrorHttpMapper"/> with default options (V2 format).
/// Use <see cref="IHttpResultResponder"/> (registered by <c>AddLimajHttpErrors</c>) to apply a
/// host's <see cref="IErrorHttpMapper"/> or <see cref="LimajHttpErrorOptions"/>.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value!);
        }

        return DefaultErrorHttpMapper.StaticFacadeDefault.Map(result.Error!);
    }

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess();
        }

        return DefaultErrorHttpMapper.StaticFacadeDefault.Map(result.Error!);
    }
}
