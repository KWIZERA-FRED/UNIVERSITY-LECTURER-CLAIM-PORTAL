FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Academic-Staff-Engagement-Claim-Processing-System/Academic-Staff-Engagement-Claim-Processing-System.csproj", "Academic-Staff-Engagement-Claim-Processing-System/"]
RUN dotnet restore "Academic-Staff-Engagement-Claim-Processing-System/Academic-Staff-Engagement-Claim-Processing-System.csproj"

COPY . .
WORKDIR "/src/Academic-Staff-Engagement-Claim-Processing-System"
RUN dotnet publish "Academic-Staff-Engagement-Claim-Processing-System.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Academic-Staff-Engagement-Claim-Processing-System.dll"]
