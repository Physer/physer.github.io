using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

[Authorize]
public class AuthenticatedModel : PageModel;