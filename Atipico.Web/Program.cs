using Atipico.Application.Models;
using Atipico.Web.Components;
using Atipico.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        // SlidingExpiration=true hace que esto sea un timeout de inactividad: la cookie se
        // renueva en cada request, y solo expira tras 30 min sin actividad.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });
// Nota: NO se usa AddAuthorization(options => options.FallbackPolicy = ...RequireAuthenticatedUser())
// aquí a propósito. Ese FallbackPolicy se aplica a TODOS los endpoints de ASP.NET Core,
// incluidos los internos de Blazor Server (/_blazor/*, negociación de SignalR) que nunca
// pasan por AuthorizeRouteView — bloquearlos rompe el circuito interactivo incluso en la
// página de login. Cada página protegida lleva su propio [Authorize] explícito en su lugar.
builder.Services.AddAuthorization();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5197";
builder.Services.AddTransient<JwtForwardingHandler>();
builder.Services.AddHttpClient("AtipicoApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/api/");
}).AddHttpMessageHandler<JwtForwardingHandler>();
builder.Services.AddScoped(typeof(IEntityApiClient<>), typeof(EntityApiClient<>));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Rutas bajo /auth/*, distintas de la página Blazor "/login": si el endpoint minimal
// comparte path exacto con un @page de un componente, ASP.NET Core ve dos endpoints
// que matchean el mismo POST y tira AmbiguousMatchException.
app.MapPost("/auth/login", async (HttpContext http, IHttpClientFactory httpClientFactory) =>
{
    var form = await http.Request.ReadFormAsync();
    var nombreUsuario = form["nombreUsuario"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var client = httpClientFactory.CreateClient("AtipicoApi");
    var response = await client.PostAsJsonAsync("auth/login", new LoginRequest
    {
        NombreUsuario = nombreUsuario,
        Password = password,
    });

    if (!response.IsSuccessStatusCode)
    {
        return Results.LocalRedirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
    if (login is null)
    {
        return Results.LocalRedirect("/login?error=1");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, login.UsuarioId.ToString()),
        new(ClaimTypes.Name, login.NombreUsuario),
        new("nombre", login.Nombre),
        new(ClaimTypes.Role, login.Rol),
        new("access_token", login.Token),
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { ExpiresUtc = login.ExpiresAt, IsPersistent = true });

    var target = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? returnUrl
        : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
