FROM nodered/node-red:4.1.13-debian

COPY --chown=node-red:node-red package.json /data/package.json
RUN npm install --prefix /data --omit=dev --no-audit --no-fund

COPY --chown=node-red:node-red settings.js /data/settings.js
COPY --chown=node-red:node-red flows.json /data/flows.json
