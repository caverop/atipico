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

// El dominio publico y el nombre del certificado administrado que lo respalda. Van como
// parametros por la misma razon que las credenciales: cambian entre entornos, y `prod` (§5.3
// de specs/deploy-azure-aspire.md) va a tener los suyos. Ninguno es secreto.
//
// `certificate-name` es el NOMBRE DEL RECURSO del certificado en el Container Apps
// Environment, no el subject ni la huella: hoy "atipico.com.bo-rg-atipi-260902203532".
var dominioPersonalizado = builder.AddParameter("custom-domain");
var nombreCertificado = builder.AddParameter("certificate-name");

// Usar PublishAsAzureContainerApp obliga a declarar el entorno de Container Apps aca: sin
// esto el AppHost aborta con "there are no 'AzureContainerAppEnvironmentResource' resources".
// Hasta ahora el entorno lo generaba azd de forma implicita y el AppHost ni se enteraba.
//
// WithAzdResourceNaming NO es opcional ni cosmetico. Aspire nombra los recursos con su propia
// convencion, distinta de la de azd, y la documentacion avisa que al migrar un despliegue que
// ya venia de azd "podes ver recursos duplicados". Con azd naming, los nombres generados
// vuelven a ser cae-/acr/law-/mi- + el token del entorno, que es exactamente lo que ya existe
// en rg-Atipico (cae-bsn3xi2hasatq y companiia). O sea: adopta lo desplegado en vez de
// levantar un entorno paralelo al lado, con otro FQDN y otro certificado.
builder.AddAzureContainerAppEnvironment("aca")
    .WithAzdResourceNaming();

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
    .WaitFor(api)
    // El dominio personalizado tiene que declararse ACA y no atarse a mano por el portal.
    // En Container Apps el certificado cuelga del Environment pero la vinculacion del hostname
    // cuelga del Container App, dentro de ingress.customDomains. Como la infraestructura de
    // atipico-web la genera azd desde este archivo (no hay bicep en el repo), cada
    // `azd provision` aplica un template donde ingress no declaraba customDomains, y ARM no
    // fusiona: reemplaza. Por eso el certificado sobrevivia y la vinculacion no, y el sitio
    // devolvia 525 -- Cloudflare abre TLS con SNI atipico.com.bo contra un origen que ya no
    // tenia certificado para ese nombre. Declarado aca, el provision lo mantiene.
    //
    // Ojo: `azd deploy` nunca rompio nada. Los dominios son del Container App y no de la
    // revision, asi que publicar una imagen los respeta. El unico que los pisa es el provision.
    // Ver specs/dominio-personalizado-azure.md.
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.ConfigureCustomDomain(dominioPersonalizado, nombreCertificado);
    });

builder.Build().Run();
