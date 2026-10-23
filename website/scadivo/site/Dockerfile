FROM nginxinc/nginx-unprivileged:stable-alpine

COPY --chown=101:101 . /usr/share/nginx/html
COPY --chown=101:101 nginx-default.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
