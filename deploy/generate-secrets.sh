#!/bin/sh
# Generates this installation's secrets on first start (Adım 31), so `docker compose up --build`
# needs nothing filled in. Runs before everything else; every other container reads what it wrote.
#
#   /secrets/raw/      one password per file, for Postgres, RabbitMQ, Seq and pgAdmin
#   /secrets/config/   one setting per file, named like its environment variable
#                      (Jwt__SigningKey, ConnectionStrings__IncidentDb) — the services read the
#                      directory as configuration (IIM_SECRETS_DIR)
#   /rabbitmq-conf/    RabbitMQ's conf.d, with its user and password
#
# A file that already exists is never rewritten, so a second start changes nothing and a secret
# added in a later version is generated on the first start after the upgrade. The volume is the
# installation's identity: deleting it means new keys, which signs everybody out and makes the
# stored customer credentials unreadable.
set -eu

raw=/secrets/raw
config=/secrets/config

mkdir -p "$raw" "$config" /rabbitmq-conf
chmod 0755 /secrets "$raw" "$config"

hex() { head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }

# Writes a file only when it is missing, without a trailing newline, readable by the non-root
# users the other containers run as.
put() {
  if [ ! -s "$1" ]; then
    printf '%s' "$2" > "$1.tmp"
    chmod 0444 "$1.tmp"
    mv "$1.tmp" "$1"
    echo "generated $(basename "$1")"
  fi
}

for name in postgres identity-db incident-db agent-db notification-db telemetry-db rabbitmq seq-admin pgadmin; do
  put "$raw/$name-password" "$(hex 24)"
done

put "$config/Jwt__SigningKey" "$(hex 32)"
put "$config/Secrets__EncryptionKey" "$(head -c 32 /dev/urandom | base64 | tr -d '\n')"
put "$config/EventBus__Password" "$(cat "$raw/rabbitmq-password")"

for service in identity incident agent notification telemetry; do
  setting=$(printf '%s' "$service" | awk '{ print toupper(substr($0, 1, 1)) substr($0, 2) }')
  put "$config/ConnectionStrings__${setting}Db" \
    "Host=postgres;Database=${service}_db;Username=${service}_user;Password=$(cat "$raw/$service-db-password")"
done

put /rabbitmq-conf/10-defaults.conf "log.console = true
"
put /rabbitmq-conf/20-iim.conf "default_user = iim
default_pass = $(cat "$raw/rabbitmq-password")
"

echo "secrets ready"
