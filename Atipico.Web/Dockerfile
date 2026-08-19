FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Atipico.Web/Atipico.Web.csproj", "Atipico.Web/"]
COPY ["Atipico.Application/Atipico.Application.csproj", "Atipico.Application/"]
COPY ["Atipico.Domain/Atipico.Domain.csproj", "Atipico.Domain/"]
RUN dotnet restore "Atipico.Web/Atipico.Web.csproj"

COPY . .
RUN dotnet publish "Atipico.Web/Atipico.Web.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Atipico.Web.dll"]
