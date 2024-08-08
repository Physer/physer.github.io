FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./demo-app ./
RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
COPY --from=build-env /publish .

FROM runtime AS development

WORKDIR /certs
COPY ./certs/cert.pem .
RUN cp cert.pem /usr/local/share/ca-certificates/cert.crt
RUN update-ca-certificates

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]

FROM runtime

WORKDIR /app
ENTRYPOINT ["dotnet", "demo-app.dll"]