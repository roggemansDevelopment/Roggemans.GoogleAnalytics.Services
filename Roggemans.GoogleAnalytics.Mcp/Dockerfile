FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Roggemans.GoogleAnalytics.Mcp/Roggemans.GoogleAnalytics.Mcp.csproj", "Roggemans.GoogleAnalytics.Mcp/"]
COPY ["Roggemans.GoogleAnalytics.Services/Roggemans.GoogleAnalytics.Services.csproj", "Roggemans.GoogleAnalytics.Services/"]
RUN dotnet restore "Roggemans.GoogleAnalytics.Mcp/Roggemans.GoogleAnalytics.Mcp.csproj"
COPY . .
WORKDIR "/src/Roggemans.GoogleAnalytics.Mcp"
RUN dotnet publish "Roggemans.GoogleAnalytics.Mcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Roggemans.GoogleAnalytics.Mcp.dll"]
