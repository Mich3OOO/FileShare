FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/FileShare.Api/FileShare.Api.csproj FileShare.Api/
RUN dotnet restore FileShare.Api/FileShare.Api.csproj

COPY src/FileShare.Api/ FileShare.Api/
RUN dotnet publish FileShare.Api/FileShare.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FileShare.Api.dll"]