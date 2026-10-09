FROM node:22.22.3-bookworm AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Ulric.ClipCalendar.sln ./
COPY src/Ulric.ClipCalendar.Api/Ulric.ClipCalendar.Api.csproj src/Ulric.ClipCalendar.Api/
RUN dotnet restore src/Ulric.ClipCalendar.Api/Ulric.ClipCalendar.Api.csproj
COPY src/ src/
COPY --from=web /src/web/dist/web/browser/ src/Ulric.ClipCalendar.Api/wwwroot/
RUN dotnet publish src/Ulric.ClipCalendar.Api/Ulric.ClipCalendar.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
COPY seed-media ./seed-media
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/data/ulric.db" \
    Storage__Root=/data \
    Seed__Enabled=true
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "Ulric.ClipCalendar.Api.dll"]
