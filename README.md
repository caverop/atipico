# Atipico

## Despliegue en Render

El repositorio se despliega como dos servicios Docker independientes:

| Servicio | Dockerfile | Variable necesaria |
| --- | --- | --- |
| API | `Atipico.Api/Dockerfile` | `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes` |
| Web | `Atipico.Web/Dockerfile` | `ApiBaseUrl` |

Conecta este repositorio como un **Blueprint** de Render para usar `render.yaml`. Render creará ambos servicios. Introduce los valores marcados como secretos en el panel de Render.

`ApiBaseUrl` debe ser la URL pública del servicio API, por ejemplo:

```text
https://atipico-api.onrender.com
```

El frontend consume la API desde el servidor Blazor, por lo que no requiere configurar CORS.