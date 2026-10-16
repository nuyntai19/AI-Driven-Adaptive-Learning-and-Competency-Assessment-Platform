# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY ["src/EduTwin.API/EduTwin.API.csproj", "src/EduTwin.API/"]
COPY ["src/EduTwin.BLL/EduTwin.BLL.csproj", "src/EduTwin.BLL/"]
COPY ["src/EduTwin.DAL/EduTwin.DAL.csproj", "src/EduTwin.DAL/"]
COPY ["src/EduTwin.Contracts/EduTwin.Contracts.csproj", "src/EduTwin.Contracts/"]
RUN dotnet restore "src/EduTwin.API/EduTwin.API.csproj"

# Copy source; publish below builds it once using the restored dependency layer.
COPY src/ src/
WORKDIR "/src/src/EduTwin.API"

# Publish stage
FROM build AS publish
RUN dotnet publish "EduTwin.API.csproj" --no-restore -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduTwin.API.dll"]
