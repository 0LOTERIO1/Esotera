# syntax=docker/dockerfile:1
# Build context: backend/ (Root Directory on Render)
# Target: .NET 9 multi-stage image for Esotera.Api

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY Esotera.Domain/Esotera.Domain.csproj Esotera.Domain/
COPY Esotera.Application/Esotera.Application.csproj Esotera.Application/
COPY Esotera.Infrastructure/Esotera.Infrastructure.csproj Esotera.Infrastructure/
COPY Esotera.Api/Esotera.Api.csproj Esotera.Api/

RUN dotnet restore Esotera.Api/Esotera.Api.csproj

FROM restore AS build
WORKDIR /src

COPY Esotera.Domain/ Esotera.Domain/
COPY Esotera.Application/ Esotera.Application/
COPY Esotera.Infrastructure/ Esotera.Infrastructure/
COPY Esotera.Api/ Esotera.Api/

RUN dotnet build Esotera.Api/Esotera.Api.csproj -c Release --no-restore

FROM build AS publish
RUN dotnet publish Esotera.Api/Esotera.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Default environment (override in Render if needed).
# Do NOT bake connection strings, JWT, or Cloudinary secrets here.
# Set them only as Render Environment Variables.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Documented container port; Render injects PORT at runtime.
EXPOSE 8080

COPY --from=publish /app/publish .

# Non-root user provided by the aspnet image
USER $APP_UID

# Bind to Render PORT (fallback 8080 locally).
# Schema: only if DB_AUTO_MIGRATE=true (default false — startup does not migrate).
ENTRYPOINT ["sh", "-c", "exec dotnet Esotera.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
