FROM debian:bookworm-slim AS build

ARG OPEN62541_VERSION=1.5.4
ARG OPEN62541_C_SHA256=9768d3f019ff67be0f9a815d0acd09a9e1906b0dcbb373512343aa41feecd87f
ARG OPEN62541_H_SHA256=b6edbc3b2d213927750af9774adc13f3bb9bbfa52199aa46f9e57cf657186afe

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gcc libc6-dev libmbedtls-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

RUN curl -fsSL "https://github.com/open62541/open62541/releases/download/v${OPEN62541_VERSION}/open62541.c" -o open62541.c \
    && curl -fsSL "https://github.com/open62541/open62541/releases/download/v${OPEN62541_VERSION}/open62541.h" -o open62541.h \
    && echo "${OPEN62541_C_SHA256}  open62541.c" | sha256sum -c - \
    && echo "${OPEN62541_H_SHA256}  open62541.h" | sha256sum -c -

COPY server.c ./server.c

# The official open62541 1.5.4 amalgamation is built with MbedTLS support.
# Keep strict C99, expose the POSIX APIs used by the amalgamation and link the
# declared crypto dependency explicitly rather than altering generated headers.
RUN gcc -std=c99 -D_GNU_SOURCE -O2 -Wall -Wextra -I. server.c open62541.c \
    -o /usr/local/bin/elitescada-opcua-peer -pthread -lm -lmbedtls -lmbedx509 -lmbedcrypto

FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends netcat-openbsd libmbedtls14 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /usr/local/bin/elitescada-opcua-peer /usr/local/bin/elitescada-opcua-peer

EXPOSE 4840

ENTRYPOINT ["/usr/local/bin/elitescada-opcua-peer"]
