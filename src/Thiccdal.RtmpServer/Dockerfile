FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 1935

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Thiccdal.slnx", "."]
COPY ["Directory.Build.props", "."]
COPY ["nuget.config", "."]
COPY ["src/", "src/"]
RUN dotnet restore "src/Thiccdal.RtmpServer/Thiccdal.RtmpServer.csproj"
WORKDIR "/src/src/Thiccdal.RtmpServer"
RUN dotnet build "Thiccdal.RtmpServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Thiccdal.RtmpServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/recordings /app/brb
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Thiccdal.RtmpServer.dll"]
