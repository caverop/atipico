
using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Services;
using Atipico.Infraestructure.Imaging;
using Atipico.Infraestructure.Persistence;
using Atipico.Infraestructure.Security;
using Atipico.Infraestructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Instrumentacion de PostgreSQL: cada comando SQL emite un span anidado bajo la peticion HTTP
// que lo origino (ver specs/telemetria-postgres.md). Va aca y no en ServiceDefaults a
// proposito: el paquete arrastra Npgsql, y ServiceDefaults lo comparte con Atipico.Web, que
// no toca la base y no tiene por que enterarse de que existe PostgreSQL.
//
// ConfigureOpenTelemetry* agrega sobre el proveedor que ya armo AddServiceDefaults(); no lo
// reemplaza. Por eso tiene que ir DESPUES: antes no hay proveedor que configurar.
//
// Ojo con los using: hacen falta TRES y ninguno es evidente por el error que dan.
//   Npgsql               -> AddNpgsql() y AddNpgsqlInstrumentation(), que NO estan en
//                           OpenTelemetry.Trace como el resto de las instrumentaciones.
//   OpenTelemetry.Trace  -> ConfigureOpenTelemetryTracerProvider
//   OpenTelemetry.Metrics-> ConfigureOpenTelemetryMeterProvider
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddNpgsql());
// Contadores del pool de conexiones. Con Neon del otro lado, quedarse corto de conexiones se
// nota tarde y mal; esto lo hace visible antes.
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddNpgsqlInstrumentation());

// Add services to the container.

builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Controladores que cargan una entidad relacionada ya trackeada en el mismo DbContext
        // (p. ej. PedidoMesasController actualizando Mesa tras crear un PedidoMesa) hacen que EF
        // rellene navegaciones bidireccionales (Mesa.PedidoMesas <-> PedidoMesa.Mesa), lo que
        // produce un ciclo al serializar. En vez de exigir [JsonIgnore] en cada navegacion
        // inversa, se cortan los ciclos con null en vez de tirar una JsonException sin capturar.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));

builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Comprobantes de pago QR (ver specs/comprobantes-qr.md). El cliente de R2 y el procesador
// de imagen no guardan estado entre peticiones, asi que van como singleton; el servicio es
// scoped porque comparte el DbContext a traves de IUnitOfWork.
builder.Services.AddSingleton<IAlmacenComprobantes, R2AlmacenComprobantes>();
builder.Services.AddSingleton<IProcesadorImagenComprobante, ImageSharpProcesadorComprobante>();
builder.Services.AddScoped<IComprobanteService, ComprobanteService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
