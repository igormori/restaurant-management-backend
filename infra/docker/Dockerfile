# Build context is the repo root: docker compose -f infra/docker/docker-compose.yml up --build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /repo
COPY src/ src/
RUN dotnet publish src/RestaurantManagement.Web -c Release -o /out/api

# One EF migration bundle per module, since each module owns its own DbContext
FROM build AS bundles
RUN dotnet tool install --global dotnet-ef --version 9.0.9
ENV PATH="$PATH:/root/.dotnet/tools"
RUN for module in Identity Organization Menu; do \
      dotnet ef migrations bundle \
        --project src/RestaurantManagement.Modules.$module \
        --startup-project src/RestaurantManagement.Web \
        --context ${module}DbContext \
        --output /out/migrations/migrate-$module; \
    done

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrator
WORKDIR /migrations
COPY --from=bundles /out/migrations .
# Bundles boot the Web host, whose startup validation needs the app settings
COPY --from=build /out/api/appsettings.json .
ENTRYPOINT ["sh", "-c", "for bundle in ./migrate-*; do $bundle --connection \"$ConnectionStrings__DefaultConnection\" || exit 1; done"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# Outside Development the app listens on Https:Port (default 8443) with the mounted certificate.
# Development keeps the base image's plain HTTP port 8080.
EXPOSE 8443 8080
ENTRYPOINT ["dotnet", "RestaurantManagement.Web.dll"]
