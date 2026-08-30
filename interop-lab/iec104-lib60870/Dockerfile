FROM alpine:3.22

RUN apk add --no-cache build-base git linux-headers

WORKDIR /opt/lib60870
RUN git clone https://github.com/mz-automation/lib60870.git . \
    && git checkout --detach 7a388e3e133999e1ca77ba7521d55d074b7cd2bc \
    && test "$(git rev-parse HEAD)" = "7a388e3e133999e1ca77ba7521d55d074b7cd2bc"

RUN make -C lib60870-C/examples/cs104_server

WORKDIR /opt/lib60870/lib60870-C/examples/cs104_server
EXPOSE 2404

CMD ["./simple_server"]
