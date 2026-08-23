# Use official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Academic-Staff-Engagement-Claim-Processing-System/Academic-Staff-Engagement-Claim-Processing-System.csproj", "Academic-Staff-Engagement-Claim-Processing-System/"]
RUN dotnet restore "Academic-Staff-Engagement-Claim-Processing-System/Academic-Staff-Engagement-Claim-Processing-System.csproj"

# Copy remaining files and build release version
COPY . .
WORKDIR "/src/Academic-Staff-Engagement-Claim-Processing-System"
RUN dotnet publish "Academic-Staff-Engagement-Claim-Processing-System.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use runtime image to execute the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose Render's default port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Academic-Staff-Engagement-Claim-Processing-System.dll"]