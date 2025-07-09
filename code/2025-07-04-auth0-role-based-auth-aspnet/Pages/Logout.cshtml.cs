using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

public class LogoutModel : PageModel
{
    public async Task OnGetAsync()
    {
        var authenticationProperties = new LogoutAuthenticationPropertiesBuilder().WithRedirectUri("/").Build();
        await HttpContext.SignOutAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}