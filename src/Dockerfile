# syntax=docker/dockerfile:1

# Create a stage for building the application.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build

COPY . /source

WORKDIR /source

# This is the architecture you’re building for, which is passed in by the builder.
# Placing it here allows the previous steps to be cached across architectures.
ARG TARGETARCH

# Build the application.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app

# Create a new stage for running the application that contains the minimal
# runtime dependencies for the application.
FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS final

RUN apk add --no-cache openssl

WORKDIR /app

# Copy everything needed to run the app from the "build" stage.
COPY --from=build /app .

RUN openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout aspnetcore.key -out aspnetcore.crt -subj "/CN=localhost" \
    && openssl pkcs12 -export -nodes -out aspnetcore.pfx -inkey aspnetcore.key -in aspnetcore.crt -passout pass:

# # Create a non-privileged user that the app will run under.
# # See https://docs.docker.com/go/dockerfile-user-best-practices/
# ARG UID=10001
# RUN adduser \
#     --disabled-password \
#     --gecos "" \
#     --home "/nonexistent" \
#     --shell "/sbin/nologin" \
#     --no-create-home \
#     --uid "${UID}" \
#     appuser

# RUN chown appuser:appuser aspnetcore.pfx

# USER appuser

EXPOSE 443

ENTRYPOINT ["dotnet", "AssetInventory.dll"]
