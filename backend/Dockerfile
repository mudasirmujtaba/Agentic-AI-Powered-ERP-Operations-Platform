# OpsPilot API — multi-stage build: SDK to publish, slim ASP.NET runtime to run (as a non-root user).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached until a project file changes.
COPY src/OpsPilot.Domain/OpsPilot.Domain.csproj src/OpsPilot.Domain/
COPY src/OpsPilot.Application/OpsPilot.Application.csproj src/OpsPilot.Application/
COPY src/OpsPilot.Infrastructure/OpsPilot.Infrastructure.csproj src/OpsPilot.Infrastructure/
COPY src/OpsPilot.Api/OpsPilot.Api.csproj src/OpsPilot.Api/
RUN dotnet restore src/OpsPilot.Api/OpsPilot.Api.csproj

COPY src/ src/
RUN dotnet publish src/OpsPilot.Api/OpsPilot.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OpsPilot.Api.dll"]
