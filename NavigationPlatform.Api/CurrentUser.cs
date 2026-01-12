using System.Security.Claims;
using NavigationPlatform.Application.Abstractions.Identity;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user is null || !user.Identity!.IsAuthenticated)
                throw new InvalidOperationException("User is not authenticated.");

            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? user.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidOperationException("Token does not contain subject (sub).");

            return Guid.Parse(sub);
        }
    }

    public bool IsAdmin =>
        _httpContextAccessor.HttpContext!.User.IsInRole("Admin");
}
