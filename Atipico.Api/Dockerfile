FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Atipico.Api/Atipico.Api.csproj", "Atipico.Api/"]
COPY ["Atipico.Application/Atipico.Application.csproj", "Atipico.Application/"]
COPY ["Atipico.Domain/Atipico.Domain.csproj", "Atipico.Domain/"]
COPY ["Atipico.Infraestructure/Atipico.Infraestructure.csproj", "Atipico.Infraestructure/"]
RUN dotnet restore "Atipico.Api/Atipico.Api.csproj"

COPY . .
# Sin --no-restore a proposito: ver el comentario equivalente en Atipico.Web/Dockerfile — un
# restore contra solo los .csproj (antes de COPY . .) deja el estado de static web assets
# incompleto para publish si no se vuelve a restaurar contra el arbol completo.
RUN dotnet publish "Atipico.Api/Atipico.Api.csproj" --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 10000

ENTRYPOINT ["dotnet", "Atipico.Api.dll"]
