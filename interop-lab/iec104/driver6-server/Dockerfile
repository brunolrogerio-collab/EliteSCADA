FROM debian:bookworm-slim AS build

ARG LIB60870_COMMIT=7a388e3e133999e1ca77ba7521d55d074b7cd2bc

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates git build-essential \
    && rm -rf /var/lib/apt/lists/*

RUN git clone https://github.com/mz-automation/lib60870.git /src/lib60870 \
    && cd /src/lib60870 \
    && git checkout --detach "${LIB60870_COMMIT}" \
    && test "$(git rev-parse HEAD)" = "${LIB60870_COMMIT}"

RUN make -C /src/lib60870/lib60870-C -j2

COPY driver6_server.c /work/driver6_server.c

RUN mkdir -p /out \
    && gcc -std=gnu99 -O2 \
       -I/src/lib60870/lib60870-C/config \
       -I/src/lib60870/lib60870-C/src/inc/api \
       -I/src/lib60870/lib60870-C/src/inc/internal \
       -I/src/lib60870/lib60870-C/src/hal/inc \
       -I/src/lib60870/lib60870-C/src/common/inc \
       /work/driver6_server.c \
       /src/lib60870/lib60870-C/build/liblib60870.a \
       -lpthread \
       -o /out/driver6-iec104-peer

FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/driver6-iec104-peer /usr/local/bin/driver6-iec104-peer

EXPOSE 2404

ENTRYPOINT ["/usr/local/bin/driver6-iec104-peer"]
