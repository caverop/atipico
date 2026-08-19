FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Atipico.Web/Atipico.Web.csproj", "Atipico.Web/"]
COPY ["Atipico.Application/Atipico.Application.csproj", "Atipico.Application/"]
COPY ["Atipico.Domain/Atipico.Domain.csproj", "Atipico.Domain/"]
RUN dotnet restore "Atipico.Web/Atipico.Web.csproj"

COPY . .
# --no-restore aca (con el restore de arriba corrido contra solo los .csproj, antes de que
# exista wwwroot/) hace que Blazor no descubra los static web assets que vienen del propio
# framework (_framework/blazor.web.js, blazor.server.js): la imagen publica sin ellos y la app
# tira 404 en runtime. Dejar que publish vuelva a restaurar (rapido, ya con los paquetes en
# cache) contra el arbol completo lo resuelve.
RUN dotnet publish "Atipico.Web/Atipico.Web.csproj" --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 10000

ENTRYPOINT ["dotnet", "Atipico.Web.dll"]
