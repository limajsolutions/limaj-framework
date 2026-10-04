namespace Limaj.Framework.Web.Http;

/// <summary>
/// The 3.x environment read behind <c>IncludeExceptionDetails = null</c> (DA-004). Kept in one
/// place so the static facades and the injected pipeline share it; removed in 4.0.0.
/// </summary>
internal static class AspNetCoreEnvironment
{
    internal static bool IsDevelopment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);
}
