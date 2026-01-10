using System.Security.Claims;
using NavigationPlatform.Application.Abstractions.Identity;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid UserId =>
        Guid.Parse(
            _http.HttpContext!.User.FindFirstValue("sub")!);

    public bool IsAdmin =>
        _http.HttpContext!.User.IsInRole("Admin");
}
