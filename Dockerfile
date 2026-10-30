# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HelpDesk.Api.csproj ./
RUN dotnet restore HelpDesk.Api.csproj
COPY . .
RUN dotnet publish HelpDesk.Api.csproj --configuration Release --no-restore --output /app/publish

# Run explicitly with the migration Compose profile; never part of API startup.
FROM build AS migration
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "--", "database", "update", "--project", "HelpDesk.Api.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HelpDesk.Api.dll"]
