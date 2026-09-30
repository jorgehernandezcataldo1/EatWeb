FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["EatWeb.csproj", "./"]

RUN dotnet restore "EatWeb.csproj"

COPY . .

RUN dotnet build "EatWeb.csproj" -c Release --no-restore

RUN dotnet publish "EatWeb.csproj" \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "EatWeb.dll"]
