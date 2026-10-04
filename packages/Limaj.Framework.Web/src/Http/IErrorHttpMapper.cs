using Limaj.Framework.Core;
using Microsoft.AspNetCore.Http;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The single place that decides how an <see cref="Error"/> is written as an HTTP response
/// (DA-002) — for failed results and for exceptions alike. Lives in Web, not Core,
/// because it returns <see cref="IResult"/>. A product decorates or inherits
/// <see cref="DefaultErrorHttpMapper"/> (e.g. to answer 422 for a given <c>Error.Code</c>) and
/// registers it as <see cref="IErrorHttpMapper"/>.
/// </summary>
public interface IErrorHttpMapper
{
    IResult Map(Error error);
}
