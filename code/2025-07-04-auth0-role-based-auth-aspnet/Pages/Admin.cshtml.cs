using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace auth0_aspnet_demo.Pages;

[Authorize(Roles = "Admin")]
public class AdminModel : PageModel;