# ---------------------------------------------------------------------------
# FamilyBudget - container image
# Multi-stage build: compile with the .NET SDK, run on the smaller ASP.NET runtime.
# ---------------------------------------------------------------------------

# --- Build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first (separate layer) so package downloads are cached between builds.
COPY FamilyBudget.slnx ./
COPY src/FamilyBudget.Web/FamilyBudget.Web.csproj src/FamilyBudget.Web/
RUN dotnet restore src/FamilyBudget.Web/FamilyBudget.Web.csproj

# Copy the rest of the source and publish a Release build.
COPY src/ src/
RUN dotnet publish src/FamilyBudget.Web/FamilyBudget.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Files checked out on a Synology share can carry mode 000 (access is granted by ACLs,
# which Docker does not copy). Make the published files readable for the non-root
# runtime user, otherwise e.g. appsettings.json gives "Permission denied".
RUN chmod -R a+rX /app/publish

# --- Runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The Git commit the image was built from; shown on the start page.
ARG APP_GIT_COMMIT=unknown
ENV APP_GIT_COMMIT=${APP_GIT_COMMIT}

# Listen on port 8080 inside the container (mapped to a NAS port in docker-compose.yml).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish ./

# Run as the non-root user that ships with the official .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "FamilyBudget.Web.dll"]
