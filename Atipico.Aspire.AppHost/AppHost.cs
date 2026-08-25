var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Atipico_Api>("atipico-api")
                 .WithExternalHttpEndpoints();

builder.AddProject<Projects.Atipico_Web>("atipico-web")
    // WithReference publica services__atipico-api__* en Atipico.Web, que es lo que el service
    // discovery necesita para resolver el nombre del recurso. Sin esto, "atipico-api" se le
    // pasa tal cual a DNS y sale "Host desconocido (atipico-api:80)" despues de ~22s de
    // reintentos de Polly.
    .WithReference(api)
    // Y ademas el ApiBaseUrl concreto. Es a proposito que la URL no viva en appsettings: el
    // AppHost es el unico que sabe donde levanto la Api, y asi Atipico.Web sigue arrancando
    // igual fuera de Aspire (docker-compose, dotnet run suelto) sin configuracion propia.
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    // Que la Web no atienda antes de que la Api este arriba: si no, el primer login que entre
    // durante el arranque falla por una carrera y no por un problema real.
    .WaitFor(api);

builder.Build().Run();
