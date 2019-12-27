FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 5000:5000/tcp
EXPOSE 5001:5001/udp
ENV UDPPort=5001
ENV WebPort=5000
ENV localURL=http://RZServer:5000

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY RZ.Server/RZ.Server.csproj RZ.Server/
COPY RZ.Server.Interfaces/RZ.Server.Interfaces.csproj RZ.Server.Interfaces/
RUN dotnet restore RZ.Server/RZ.Server.csproj
COPY . .
WORKDIR /src/RZ.Server
RUN dotnet build RZ.Server.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish RZ.Server.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "RZ.Server.dll"]
