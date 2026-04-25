FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/OrderProcessing.API/OrderProcessing.API.csproj
RUN dotnet publish src/OrderProcessing.API/OrderProcessing.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OrderProcessing.API.dll"]