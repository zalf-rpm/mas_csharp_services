# Multi-stage build for ServiceRegistry solution
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set build arguments (can be overridden at build time)
ARG CONFIGURATION=Release
ARG PROJECT=Mas.Infrastructure.ServiceRegistry/Mas.Infrastructure.ServiceRegistry.csproj

WORKDIR /build

# Copy only solution and project files first to leverage Docker layer caching for restore
COPY ServiceRegistry.sln ./
COPY mas_csharp_common/*.csproj mas_csharp_common/
COPY Mas.Infrastructure.ServiceRegistry/*.csproj Mas.Infrastructure.ServiceRegistry/
COPY Mas.Infrastructure.ServiceRegistry.Test/*.csproj Mas.Infrastructure.ServiceRegistry.Test/
COPY mas_capnproto_schemas/gen/csharp/*.csproj mas_capnproto_schemas/gen/csharp/
COPY capnproto-dotnetcore/Capnp.Net.Runtime/*.csproj capnproto-dotnetcore/Capnp.Net.Runtime/

# Restore dependencies
RUN dotnet restore ServiceRegistry.sln

# Copy the rest of the repository
COPY . .

# Publish (framework-dependent, uses app host for a simple ./Mas.Infrastructure.ServiceRegistry entrypoint)
RUN dotnet publish "$PROJECT" -c $CONFIGURATION -o /app/publish \
    -p:UseAppHost=true \
    --no-self-contained

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Environment hardening / tuning (adjust as needed)
ENV DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    TZ=UTC

# Expose a port only if the service actually listens (adjust/remove if not applicable)
EXPOSE 8080

ENTRYPOINT ["./Mas.Infrastructure.ServiceRegistry"]
