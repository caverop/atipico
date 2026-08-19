FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Atipico.Api/Atipico.Api.csproj", "Atipico.Api/"]
COPY ["Atipico.Application/Atipico.Application.csproj", "Atipico.Application/"]
COPY ["Atipico.Domain/Atipico.Domain.csproj", "Atipico.Domain/"]
COPY ["Atipico.Infraestructure/Atipico.Infraestructure.csproj", "Atipico.Infraestructure/"]
RUN dotnet restore "Atipico.Api/Atipico.Api.csproj"

COPY . .
RUN dotnet publish "Atipico.Api/Atipico.Api.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 10000

ENTRYPOINT ["dotnet", "Atipico.Api.dll"]
