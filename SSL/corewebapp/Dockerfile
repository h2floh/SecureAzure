FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
# Set your workdir
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# build runtime image core/aspnet -> ASP.NET core/runtime -> .NET Core
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app

# Copy from build
COPY --from=build-env /app/out .

# EXPOSE PORT
EXPOSE 5001

# Run your app (dll name has to be changed)
ENTRYPOINT ["dotnet", "corewebapp.dll"]

