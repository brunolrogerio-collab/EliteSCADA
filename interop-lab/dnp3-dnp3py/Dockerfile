FROM python:3.13-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/dnp3py
RUN git clone https://github.com/craigpnnl/dnp3py.git . \
    && git checkout --detach 8a20d4c276274f2b98800716cd7da963f21da2c1 \
    && test "$(git rev-parse HEAD)" = "8a20d4c276274f2b98800716cd7da963f21da2c1"

ENV PYTHONPATH=/opt/dnp3py/src
COPY interop-lab/dnp3-dnp3py/outstation.py /opt/lab/outstation.py

EXPOSE 20000
CMD ["python", "/opt/lab/outstation.py"]
