#!/usr/bin/env bash
# Checks a running stack (docker compose up) through the web container.
# Usage: scripts/smoke-test.sh [base-url]   (default http://localhost:8080)
set -euo pipefail

BASE=${1:-http://localhost:8080}

# check <path> <text>: the response is 2xx and its body contains <text>.
check() {
    if curl -fsS "$BASE$1" | grep -qF "$2"; then
        echo "ok   $1"
    else
        echo "FAIL $1: expected a 2xx response containing $2" >&2
        exit 1
    fi
}

# The API migrates and seeds on start, so wait for it first.
if ! curl -fs -o /dev/null --retry 45 --retry-delay 2 --retry-max-time 90 --retry-all-errors "$BASE/api/rooms"; then
    echo "FAIL /api/rooms: no 2xx response from $BASE after 90 s" >&2
    exit 1
fi

check /api/rooms '"R6"'
check '/api/sessions?date=2026-03-06' '"lessonId":"L018"'
check /tutors/T1 '<div id="root">'

status=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/does-not-exist")
if [ "$status" = 404 ]; then
    echo "ok   /api/does-not-exist (404)"
else
    echo "FAIL /api/does-not-exist: expected 404, got $status" >&2
    exit 1
fi
