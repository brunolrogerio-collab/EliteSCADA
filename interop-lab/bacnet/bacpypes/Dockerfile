FROM python:3.9-slim

RUN python -m pip download --no-deps --only-binary=:all: bacpypes==0.19.0 -d /tmp/wheels \
    && echo "317fbe440474845978dab134f8aefb5f45a41a13926cbdf87b9b8afe6e79131b  /tmp/wheels/bacpypes-0.19.0-py3-none-any.whl" | sha256sum -c - \
    && python -m pip install --no-cache-dir /tmp/wheels/bacpypes-0.19.0-py3-none-any.whl \
    && rm -rf /tmp/wheels

COPY server.py /opt/lab/server.py

ENTRYPOINT ["python", "/opt/lab/server.py"]
