FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env

WORKDIR /app

COPY ./src ./

RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0

WORKDIR /app

COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "rinha-de-backend-q3-fsharp.dll"]