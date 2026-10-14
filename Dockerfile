# --- Web frontend ---
FROM docker.io/library/node:24-alpine AS web
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# --- API ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src
COPY backend/Directory.Packages.props ./
COPY backend/OpenTablo.Api/OpenTablo.Api.csproj OpenTablo.Api/
RUN dotnet restore OpenTablo.Api/OpenTablo.Api.csproj
COPY backend/OpenTablo.Api/ OpenTablo.Api/
RUN dotnet publish OpenTablo.Api/OpenTablo.Api.csproj -c Release -o /app --no-restore

# --- Runtime: the API also serves the web build ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/dist ./wwwroot
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenTablo.Api.dll"]
