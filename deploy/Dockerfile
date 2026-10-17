# Hollow Wake MMO authoritative server. Build from the repository root:
#   docker build -f deploy/Dockerfile -t hollowwake-server .
# Run (data persists in the mounted volume):
#   docker run -d --name hollowwake -p 7777:7777 -v hollowwake-data:/app/data hollowwake-server
#
# HOST=brutal builds the BRUTAL/RakNet host (UDP 7777) instead of the pure .NET TCP host.
ARG HOST=tcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG HOST
WORKDIR /src
COPY global.json Directory.Build.props HollowWake.sln ./
COPY src/HollowWake.Core/HollowWake.Core.csproj src/HollowWake.Core/
COPY src/HollowWake.Protocol/HollowWake.Protocol.csproj src/HollowWake.Protocol/
COPY src/HollowWake.Server/HollowWake.Server.csproj src/HollowWake.Server/
COPY src/HollowWake.Server.Host/HollowWake.Server.Host.csproj src/HollowWake.Server.Host/
COPY src/HollowWake.Server.Host.Brutal/HollowWake.Server.Host.Brutal.csproj src/HollowWake.Server.Host.Brutal/
COPY src/HollowWake.Tools/HollowWake.Tools.csproj src/HollowWake.Tools/
RUN dotnet restore src/HollowWake.Server.Host/HollowWake.Server.Host.csproj \
 && dotnet restore src/HollowWake.Server.Host.Brutal/HollowWake.Server.Host.Brutal.csproj \
 && dotnet restore src/HollowWake.Tools/HollowWake.Tools.csproj
COPY src/ src/
RUN if [ "$HOST" = "brutal" ]; then \
      dotnet publish src/HollowWake.Server.Host.Brutal/HollowWake.Server.Host.Brutal.csproj -c Release -r linux-x64 --self-contained false -o /out; \
    else \
      dotnet publish src/HollowWake.Server.Host/HollowWake.Server.Host.csproj -c Release -o /out; \
    fi \
 && dotnet publish src/HollowWake.Tools/HollowWake.Tools.csproj -c Release -o /out/tools

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG HOST
WORKDIR /app
COPY --from=build /out/ ./
COPY content/ ./content/
COPY config/server.yaml ./config/server.yaml
RUN mkdir -p /app/data && useradd -r -s /usr/sbin/nologin hollowwake && chown -R hollowwake:hollowwake /app
USER hollowwake
ENV DOTNET_gcServer=0 DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 7777/tcp 7777/udp
VOLUME ["/app/data"]
ENV HOLLOWWAKE_HOST=${HOST}
ENTRYPOINT ["/bin/sh", "-c", "if [ \"$HOLLOWWAKE_HOST\" = brutal ]; then exec dotnet hollowwake-server-brutal.dll --root /app \"$@\"; else exec dotnet hollowwake-server.dll --root /app \"$@\"; fi", "--"]
