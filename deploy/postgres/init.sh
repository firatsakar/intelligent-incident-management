#!/bin/bash
# Runs once, when the Postgres volume is first created: one database and one owner per
# service. One server, but no service can read another's data — each connects as its own user and
# owns only its own database, which is the database-per-service rule without five servers.
#
# The passwords are the ones the `secrets` container generated, read from the files
# docker-compose.yml points the *_PASSWORD_FILE variables at. They are set once: this script does
# not run again on an existing volume.
#
# No `set -u`: should the file lose its executable bit, the image sources it instead of running it,
# and -u would outlive it in the image's own entrypoint.
set -eo pipefail

create() {
  local user=$1 database=$2 password_file=$3
  local password
  password=$(cat "$password_file")

  # The password goes in as a psql variable and is quoted by psql itself (:'password'), so a
  # quote or a backslash in it cannot end the statement early.
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
    -v user="$user" -v database="$database" -v password="$password" <<-'SQL'
	CREATE USER :"user" WITH PASSWORD :'password';
	CREATE DATABASE :"database" OWNER :"user";
	REVOKE ALL ON DATABASE :"database" FROM PUBLIC;
SQL
}

create identity_user identity_db "$IDENTITY_DB_PASSWORD_FILE"
create incident_user incident_db "$INCIDENT_DB_PASSWORD_FILE"
create agent_user agent_db "$AGENT_DB_PASSWORD_FILE"
create notification_user notification_db "$NOTIFICATION_DB_PASSWORD_FILE"
create telemetry_user telemetry_db "$TELEMETRY_DB_PASSWORD_FILE"
