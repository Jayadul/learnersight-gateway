FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln .
COPY NetZeroGateway.Domain/NetZeroGateway.Domain.csproj           NetZeroGateway.Domain/
COPY NetZeroGateway.Infrastructure/NetZeroGateway.Infrastructure.csproj   NetZeroGateway.Infrastructure/
COPY NetZeroGateway.Application/NetZeroGateway.Application.csproj NetZeroGateway.Application/
COPY NetZeroGateway.API/NetZeroGateway.API.csproj                 NetZeroGateway.API/
RUN dotnet restore
COPY . .
RUN dotnet publish NetZeroGateway.API -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NetZeroGateway.API.dll"]
