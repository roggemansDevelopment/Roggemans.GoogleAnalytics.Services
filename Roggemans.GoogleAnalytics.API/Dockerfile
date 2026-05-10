# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Roggemans.GoogleAnalytics.Services.sln", "./"]
COPY ["Roggemans.GoogleAnalytics.Services/Roggemans.GoogleAnalytics.Services.csproj", "Roggemans.GoogleAnalytics.Services/"]
COPY ["Roggemans.GoogleAnalytics.API/Roggemans.GoogleAnalytics.API.csproj", "Roggemans.GoogleAnalytics.API/"]

RUN dotnet restore "Roggemans.GoogleAnalytics.API/Roggemans.GoogleAnalytics.API.csproj"

COPY . .
RUN dotnet publish "Roggemans.GoogleAnalytics.API/Roggemans.GoogleAnalytics.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Roggemans.GoogleAnalytics.API.dll"]
