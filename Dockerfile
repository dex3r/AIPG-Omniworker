# Dotnet Installer image
FROM amd64/buildpack-deps:jammy-curl AS installer

# Retrieve ASP.NET Core
RUN aspnetcore_version=8.0.7 \
    && curl -fSL --output aspnetcore.tar.gz https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$aspnetcore_version/aspnetcore-runtime-$aspnetcore_version-linux-x64.tar.gz \
    && aspnetcore_sha512='c7479dc008fce77c2bfcaa1ac1c9fe6f64ef7e59609fff6707da14975aade73e3cb22b97f2b3922a2642fa8d843a3caf714ab3a2b357abeda486b9d0f8bebb18' \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -oxzf aspnetcore.tar.gz ./shared/Microsoft.AspNetCore.App \
    && rm aspnetcore.tar.gz

RUN dotnet_version=8.0.7 \
    && curl -fSL --output dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-linux-x64.tar.gz \
    && dotnet_sha512='88e9ac34ad5ac76eec5499f2eb8d1aa35076518c842854ec1053953d34969c7bf1c5b2dbce245dbace3a18c3b8a4c79d2ef2d2ff105ce9d17cbbdbe813d8b16f' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /dotnet \
    && tar -oxzf dotnet.tar.gz -C /dotnet \
    && rm dotnet.tar.gz

# Aphroride image
FROM alpindale/aphrodite-engine AS base

USER root
RUN apt-get -y update && apt-get install -y libicu-dev wget libgl1 libjemalloc2 dos2unix curl

# Image Worker

WORKDIR /image-worker

# This takes minutes, so it has to be as high as possible in the Dockerfile to be cached as frequently as possible
COPY image-worker/update-runtime.sh image-worker/environment.yaml image-worker/requirements.txt .
RUN /image-worker/update-runtime.sh

COPY image-worker/ .

# .sh scripts fail in linux when end of lines are not set to LF
# This can be solved on git on per-checkout basis, but this is much safer
RUN find . -type f -name "*.sh" -exec dos2unix {} \+;

# Dotnet app
# ASP.NET Core version
ENV ASPNET_VERSION=8.0.7

WORKDIR /

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]
COPY --from=installer ["/shared/Microsoft.AspNetCore.App", "/usr/share/dotnet/shared/Microsoft.AspNetCore.App"]

RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Text Worker

WORKDIR /worker

COPY grid-text-worker/requirements-scribe.txt .

RUN pip install --break-system-packages --no-cache-dir -r requirements-scribe.txt

COPY grid-text-worker/ .

EXPOSE 443

#CMD ["python", "-s", "bridge_scribe.py"]

# Silly Tavern

WORKDIR /silly-tavern
COPY silly-tavern/ .

#USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Aipg-omniworker-dotnet/AipgOmniworker/AipgOmniworker.csproj", "AipgOmniworker/"]
RUN dotnet restore "AipgOmniworker/AipgOmniworker.csproj"
COPY Aipg-omniworker-dotnet/ .
WORKDIR "/src/AipgOmniworker"
RUN dotnet build "AipgOmniworker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AipgOmniworker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AipgOmniworker.dll"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]