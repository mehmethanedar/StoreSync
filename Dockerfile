FROM node:22-bookworm-slim AS web-build
WORKDIR /web
ENV NG_CLI_ANALYTICS=false
COPY client/package*.json client/.npmrc ./
RUN npm ci
COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /source
COPY UpdateApi/UpdateApi.csproj ./UpdateApi/
RUN dotnet restore UpdateApi/UpdateApi.csproj
COPY UpdateApi/ ./UpdateApi/
RUN dotnet publish UpdateApi/UpdateApi.csproj -c Release -o /publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api-build /publish ./
COPY --from=web-build /web/dist/browser ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production \
    URLS=http://0.0.0.0:10000 \
    Demo__SourceBaseUrl=http://127.0.0.1:10000 \
    AllowedHosts="*.onrender.com;localhost;127.0.0.1" \
    STORESYNC_DATA=/app/DemoData
RUN mkdir -p /app/DemoData && chown app:app /app/DemoData
USER app
EXPOSE 10000
ENTRYPOINT ["dotnet", "UpdateApi.dll"]
