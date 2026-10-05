# Build context: the repo root (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props ./
COPY src/ src/
# Only the API project, so the web's .esproj and Node are not needed here.
RUN dotnet publish src/BrightPath.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.source=https://github.com/duytech/TutoringSchedulingSDD
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
# The image listens on 8080 by default.
ENTRYPOINT ["dotnet", "BrightPath.Api.dll"]
