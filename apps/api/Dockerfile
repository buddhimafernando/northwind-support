# The Support Desk API. Build from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# Nothing environment-specific is baked in: the connection string, Jwt__SigningKey and
# ASPNETCORE_ENVIRONMENT are supplied when the container starts (see docker-compose.yml).

# ---- build: .NET SDK ---------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from only the files that decide what gets restored, so this layer - the slow one -
# is reused until a project file or a package version changes, not on every .cs edit.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
        --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---- runtime: ASP.NET Core runtime only ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Plain HTTP on 8080; TLS belongs to whatever sits in front of the container.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .

# The image's built-in unprivileged "app" user. The files above stay owned by root, so the
# process can read its own binaries but not change them.
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
