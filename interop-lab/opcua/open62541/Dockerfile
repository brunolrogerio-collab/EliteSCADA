FROM debian:bookworm-slim AS build

ARG OPEN62541_VERSION=1.5.7
ARG OPEN62541_C_SHA256=103ff486a915c8d019bae81f8f05aa16e5f43629dc323bac43175ccb90df46fe
ARG OPEN62541_H_SHA256=59e43ea4d0d3e5511a1e9fb89a9d44d5a4345d149d2f3da9749f1072405bacdd

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gcc libc6-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

RUN curl -fsSL "https://github.com/open62541/open62541/releases/download/v${OPEN62541_VERSION}/open62541.c" -o open62541.c \
    && curl -fsSL "https://github.com/open62541/open62541/releases/download/v${OPEN62541_VERSION}/open62541.h" -o open62541.h \
    && echo "${OPEN62541_C_SHA256}  open62541.c" | sha256sum -c - \
    && echo "${OPEN62541_H_SHA256}  open62541.h" | sha256sum -c -

COPY server.c ./server.c

RUN gcc -std=c99 -O2 -Wall -Wextra -I. server.c open62541.c \
    -o /usr/local/bin/elitescada-opcua-peer -pthread -lm

FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /usr/local/bin/elitescada-opcua-peer /usr/local/bin/elitescada-opcua-peer

EXPOSE 4840

ENTRYPOINT ["/usr/local/bin/elitescada-opcua-peer"]
