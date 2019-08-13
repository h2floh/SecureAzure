FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
# Set your workdir
WORKDIR /app

# copy csproj and restore as distinct layers
COPY . ./
RUN rm *.json
RUN rm *.md
RUN cd /app/AADLoginSamplePersonalAndAllOrg && dotnet restore

# copy everything else and build
RUN cd /app/AADLoginSamplePersonalAndAllOrg && dotnet publish -c Release -o out

# build runtime image core/aspnet -> ASP.NET core/runtime -> .NET Core
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app

# Copy from build
COPY --from=build-env /app/AADLoginSamplePersonalAndAllOrg/out .

# EXPOSE PORT
EXPOSE 5001

# Run your app (dll name has to be changed)
ENTRYPOINT ["dotnet", "AADLoginSamplePersonalAndAllOrg.dll"]

