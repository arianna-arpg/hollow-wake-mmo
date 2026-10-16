# Skein authoritative server. Build from the repository root:
#   docker build -f deploy/Dockerfile -t skein-server .
# Run (data persists in the mounted volume):
#   docker run -d --name skein -p 7777:7777 -v skein-data:/app/data skein-server
#
# HOST=brutal builds the BRUTAL/RakNet host (UDP 7777) instead of the pure .NET TCP host.
ARG HOST=tcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG HOST
WORKDIR /src
COPY global.json Directory.Build.props Skein.sln ./
COPY src/Skein.Core/Skein.Core.csproj src/Skein.Core/
COPY src/Skein.Protocol/Skein.Protocol.csproj src/Skein.Protocol/
COPY src/Skein.Server/Skein.Server.csproj src/Skein.Server/
COPY src/Skein.Server.Host/Skein.Server.Host.csproj src/Skein.Server.Host/
COPY src/Skein.Server.Host.Brutal/Skein.Server.Host.Brutal.csproj src/Skein.Server.Host.Brutal/
COPY src/Skein.Tools/Skein.Tools.csproj src/Skein.Tools/
RUN dotnet restore src/Skein.Server.Host/Skein.Server.Host.csproj \
 && dotnet restore src/Skein.Server.Host.Brutal/Skein.Server.Host.Brutal.csproj \
 && dotnet restore src/Skein.Tools/Skein.Tools.csproj
COPY src/ src/
RUN if [ "$HOST" = "brutal" ]; then \
      dotnet publish src/Skein.Server.Host.Brutal/Skein.Server.Host.Brutal.csproj -c Release -r linux-x64 --self-contained false -o /out; \
    else \
      dotnet publish src/Skein.Server.Host/Skein.Server.Host.csproj -c Release -o /out; \
    fi \
 && dotnet publish src/Skein.Tools/Skein.Tools.csproj -c Release -o /out/tools

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG HOST
WORKDIR /app
COPY --from=build /out/ ./
COPY content/ ./content/
COPY config/server.yaml ./config/server.yaml
RUN mkdir -p /app/data && useradd -r -s /usr/sbin/nologin skein && chown -R skein:skein /app
USER skein
ENV DOTNET_gcServer=0 DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 7777/tcp 7777/udp
VOLUME ["/app/data"]
ENV SKEIN_HOST=${HOST}
ENTRYPOINT ["/bin/sh", "-c", "if [ \"$SKEIN_HOST\" = brutal ]; then exec dotnet skein-server-brutal.dll --root /app \"$@\"; else exec dotnet skein-server.dll --root /app \"$@\"; fi", "--"]
