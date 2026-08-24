using Atipico.Application.Models;
using Atipico.Web.Components;
using Atipico.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Json;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

// De donde sale ApiBaseUrl, en orden de precedencia real:
//   - Aspire: lo inyecta el AppHost con el endpoint concreto de atipico-api (ver AppHost.cs).
//   - docker-compose: variable de entorno, http://api:10000.
//   - dotnet run suelto: ninguno de los dos, y cae en este default.
// A proposito NO vive en appsettings: si ahi se fija el nombre del recurso de Aspire, correr
// Atipico.Web sin el AppHost se rompe con "Host desconocido" y cuesta ver por que.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5197";
builder.Services.AddTransient<JwtForwardingHandler>();
builder.Services.AddHttpClient("AtipicoApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/api/");
}).AddHttpMessageHandler<JwtForwardingHandler>();
// Los clientes reales se registran por su tipo concreto y la interfaz apunta al decorador
// que reporta el progreso. Asi cualquier pagina que inyecte la interfaz enciende la barra
// sin codigo propio. Un HttpClient crudo, en cambio, la evita: ver CLAUDE.md.
// Data Protection cifra la cookie de autenticacion y el token antiforgery. Sin esto, el
// llavero se guarda en el home del usuario del contenedor, que se pierde en cada
// recreacion: al levantar de nuevo, ASP.NET Core genera claves nuevas y todo lo cifrado con
// las anteriores deja de poder descifrarse. Eso es lo que produce "The antiforgery token
// could not be decrypted" y, peor, cierra la sesion de todos los usuarios en cada deploy.
//
// La ruta se puede montar como volumen (ver docker-compose.yml) o como disco persistente.
// Si no se monta nada, escribe dentro del contenedor y el comportamiento es el de antes: no
// empeora nada donde no haya donde persistir.
builder.Services.AddDataProtection()
    // Fijo y explicito: por defecto se deriva de la ruta del contenido, que cambia entre
    // entornos y haria ilegibles las claves aunque el directorio si se conserve.
    .SetApplicationName("Atipico")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"] ?? "/var/atipico/keys"));

builder.Services.AddScoped<EstadoOperaciones>();

// Resolver enlaces cortos de Google Maps (docs/enlace-corto-ubicacion.md). Cliente aparte del
// de la API: no lleva el token del usuario, y sobre todo NO sigue redirects solo — cada salto
// se valida antes de seguirlo. El timeout corto no es prudencia teorica: el cajero espera con
// el comensal enfrente.
builder.Services.AddHttpClient(ResolvedorEnlaceUbicacion.ClienteHttp, client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Atipico/1.0 (sistema de restaurante)");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddScoped<IResolvedorEnlaceUbicacion, ResolvedorEnlaceUbicacion>();

builder.Services.AddScoped(typeof(EntityApiClient<>));
builder.Services.AddScoped(typeof(IEntityApiClient<>), typeof(EntityApiClientConProgreso<>));

// Comprobantes: multipart y endpoints propios, fuera del contrato CRUD generico.
builder.Services.AddScoped<ComprobanteApiClient>();
builder.Services.AddScoped<IComprobanteApiClient, ComprobanteApiClientConProgreso>();

var app = builder.Build();

app.MapDefaultEndpoints();

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
