FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["EXDWorker/EXDWorker.csproj", "EXDWorker/"]
RUN dotnet restore "EXDWorker/EXDWorker.csproj"
COPY . .
WORKDIR "/src/EXDWorker"
RUN dotnet build "EXDWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EXDWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EXDWorker.dll"]
