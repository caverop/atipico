var builder = DistributedApplication.CreateBuilder(args);

// Credenciales: un parametro por cada valor que sea secreto o que cambie entre entornos, en
// vez de leerlos de appsettings o del entorno de azd directamente. Un parametro se resuelve
// distinto segun donde corra el AppHost, y esa es toda la gracia:
//
//   aspire run / F5 -> "Parameters:<nombre>" de la configuracion del AppHost, que en la
//                      practica son sus user-secrets (UserSecretsId ya esta en el .csproj).
//   azd up          -> el valor del entorno azd activo. Con secret:true azd lo guarda en Key
//                      Vault y se lo pasa al Container App como secretRef; sin secret:true
//                      queda como variable de entorno comun, que es lo correcto para lo que
//                      no es secreto.
//
// El mismo archivo sirve para dev y para prod sin ramas, y no queda ninguna credencial en el
// repositorio. Cambiar de entorno es "azd env select <nombre>", no editar codigo.
var cadenaConexion = builder.AddParameter("db-connection-string", secret: true);
var jwtKey = builder.AddParameter("jwt-key", secret: true);
var r2AccountId = builder.AddParameter("r2-account-id");
var r2AccessKeyId = builder.AddParameter("r2-access-key-id", secret: true);
var r2SecretAccessKey = builder.AddParameter("r2-secret-access-key", secret: true);
var r2Bucket = builder.AddParameter("r2-bucket");

var api = builder.AddProject<Projects.Atipico_Api>("atipico-api")
    // Doble guion bajo y no dos puntos: es como el proveedor de variables de entorno de .NET
    // expresa una seccion anidada. "Jwt:Key" no es un nombre de variable valido en Linux, y
    // el contenedor arranca igual pero sin ver el valor: exactamente el fallo que dejaba la
    // revision en ActivationFailed con "Falta configurar Jwt:Key.".
    .WithEnvironment("ConnectionStrings__DefaultConnection", cadenaConexion)
    .WithEnvironment("Jwt__Key", jwtKey)
    // Issuer y Audience no son secretos ni cambian entre entornos: van literales. Hacerlos
    // parametros solo agregaria dos preguntas al `azd up` sin comprar nada.
    .WithEnvironment("Jwt__Issuer", "Atipico.Api")
    .WithEnvironment("Jwt__Audience", "Atipico.Clients")
    .WithEnvironment("Jwt__ExpiryMinutes", "480")
    .WithEnvironment("R2__AccountId", r2AccountId)
    .WithEnvironment("R2__AccessKeyId", r2AccessKeyId)
    .WithEnvironment("R2__SecretAccessKey", r2SecretAccessKey)
    // El bucket si cambia entre entornos (atipico-comprobantes-dev vs. el de produccion), y
    // no es secreto: parametro sin secret:true.
    .WithEnvironment("R2__Bucket", r2Bucket);

builder.AddProject<Projects.Atipico_Web>("atipico-web")
    .WithExternalHttpEndpoints()
    // El llavero de Data Protection. El default del codigo es /var/atipico/keys, que sirve en
    // docker-compose porque ahi hay un volumen montado; en Container Apps no hay nada montado
    // y el contenedor no corre como root, asi que ni siquiera puede crear el directorio y
    // Blazor se cae en el primer render (prerenderiza el estado de los componentes con
    // Protect, no solo la cookie). /tmp si es escribible.
    //
    // PROVISIONAL: las claves viven en el contenedor, o sea que se pierden en cada revision
    // (todos los usuarios vuelven a loguearse tras cada deploy) y cada replica tiene las
    // suyas (maxReplicas esta en 10). Por eso WithReplicas(1): con una sola replica el
    // llavero es consistente mientras dura. El arreglo de verdad es persistirlo afuera.
    .WithEnvironment("DataProtection__KeysPath", "/tmp/atipico-keys")
    .WithReplicas(1)
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
