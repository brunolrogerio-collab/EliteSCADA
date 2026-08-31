FROM python:3.13-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

RUN python -m pip download --no-deps --only-binary=:all: python-snap7==3.1.2 -d /tmp/wheels \
    && echo "17a486966d415e54e0b8e2dad3204323e16c445fbc55af44d9cd3173c9573de2  /tmp/wheels/python_snap7-3.1.2-py3-none-any.whl" | sha256sum -c - \
    && python -m pip install --no-cache-dir /tmp/wheels/python_snap7-3.1.2-py3-none-any.whl \
    && rm -rf /tmp/wheels

COPY server.py /opt/lab/server.py

EXPOSE 102

ENTRYPOINT ["python", "/opt/lab/server.py"]
