# ---- Build stage: full SDK, compiles the app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first and restore. This layer is cached and
# reused on every rebuild UNLESS a .csproj changes, so you don't re-download
# NuGet packages each time you edit code.
COPY Api/Api.csproj Api/
COPY Business/Business.csproj Business/
COPY DataAccess/DataAccess.csproj DataAccess/
RUN dotnet restore Api/Api.csproj

# Now bring in the rest of the source and publish (Release, no re-restore).
COPY Api/ Api/
COPY Business/ Business/
COPY DataAccess/ DataAccess/
# -p:OpenApiGenerateDocumentsOnBuild=false : the Microsoft.Extensions.ApiDescription.Server
# package normally BOOTS the app at build time to emit the OpenAPI spec, but the app throws
# on startup without its env-var secrets (which a clean container build doesn't have). We skip
# that step — Scalar/OpenAPI still work at runtime; only the build-time file generation is off.
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore -p:OpenApiGenerateDocumentsOnBuild=false

# ---- Runtime stage: small aspnet image, no SDK/compiler ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
