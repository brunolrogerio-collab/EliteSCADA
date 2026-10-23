FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY portal/Scadivo.Portal.csproj portal/
RUN dotnet restore portal/Scadivo.Portal.csproj
COPY portal/ portal/
RUN dotnet publish portal/Scadivo.Portal.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13
WORKDIR /app
COPY --from=build --chown=1654:1654 /publish .
USER 1654
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=6s --start-period=40s --retries=3 CMD ["dotnet", "Scadivo.Portal.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Scadivo.Portal.dll"]
