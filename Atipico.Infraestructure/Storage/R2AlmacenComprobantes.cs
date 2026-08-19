using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Atipico.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Atipico.Infraestructure.Storage
{
    /// <summary>
    /// Adaptador contra Cloudflare R2, que habla el protocolo S3. El bucket es privado:
    /// no tiene dominio publico ni r2.dev, y la unica forma de leer un objeto es una URL
    /// firmada de vida corta que emite la API despues de validar el rol.
    /// </summary>
    public class R2AlmacenComprobantes : IAlmacenComprobantes, IDisposable
    {
        private readonly IAmazonS3 _cliente;
        private readonly string _bucket;

        public R2AlmacenComprobantes(IConfiguration configuration)
        {
            var seccion = configuration.GetSection("R2");

            var accountId = seccion["AccountId"]
                ?? throw new InvalidOperationException("Falta configurar R2:AccountId.");
            var accessKeyId = seccion["AccessKeyId"]
                ?? throw new InvalidOperationException("Falta configurar R2:AccessKeyId.");
            var secretAccessKey = seccion["SecretAccessKey"]
                ?? throw new InvalidOperationException("Falta configurar R2:SecretAccessKey.");
            _bucket = seccion["Bucket"]
                ?? throw new InvalidOperationException("Falta configurar R2:Bucket.");

            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                // R2 no expone buckets como subdominio del endpoint.
                ForcePathStyle = true,
                // R2 no tiene regiones, pero SigV4 necesita uno para firmar.
                AuthenticationRegion = "auto",
            };

            _cliente = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
        }

        public async Task GuardarAsync(string clave, Stream contenido, string tipoContenido, CancellationToken ct = default)
        {
            var solicitud = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = clave,
                InputStream = contenido,
                ContentType = tipoContenido,
                // R2 rechaza la firma de payload en streaming (STREAMING-AWS4-HMAC-SHA256).
                DisablePayloadSigning = true,
            };

            await _cliente.PutObjectAsync(solicitud, ct);
        }

        public string GenerarUrlFirmada(string clave, TimeSpan duracion)
        {
            return _cliente.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = clave,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(duracion),
            });
        }

        public void Dispose()
        {
            _cliente.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
