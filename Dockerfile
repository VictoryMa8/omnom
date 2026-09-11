# Stage 1: Build the Vue Frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# Stage 2: Build the .NET Web API Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app/backend

COPY backend/*.csproj ./
RUN dotnet restore

COPY backend/ ./
# Copy built frontend assets to wwwroot
COPY --from=frontend-build /app/backend/wwwroot ./wwwroot

RUN dotnet publish -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Ensure SQLite directory exists
RUN mkdir -p /data
ENV DATA_DIR=/data
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080

EXPOSE 8080

COPY --from=backend-build /app/publish ./

VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Omnom.Api.dll"]
