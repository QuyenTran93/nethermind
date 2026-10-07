#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

commit_hash="$(git rev-parse HEAD)"
build_timestamp="$(date '+%s')"

if docker image inspect nethermind:1.29.1 >/dev/null 2>&1; then
  docker tag nethermind:1.29.1 nethermind:1.29.1-old
  echo "tagged nethermind:1.29.1 -> nethermind:1.29.1-old"
else
  echo "WARN: nethermind:1.29.1 not present; skip retag" >&2
fi

echo "building nethermind:1.29.1 (live fork block 36333941)"
docker build -f Dockerfile \
  --build-arg BUILD_CONFIG=release \
  --build-arg BUILD_TIMESTAMP="$build_timestamp" \
  --build-arg COMMIT_HASH="$commit_hash" \
  --build-arg BALANCE_FORK_PROFILE=Live \
  --tag nethermind:1.29.1 \
  .

echo "building nethermind:1.29.1-test (fork block 20)"
docker build -f Dockerfile \
  --build-arg BUILD_CONFIG=release \
  --build-arg BUILD_TIMESTAMP="$build_timestamp" \
  --build-arg COMMIT_HASH="$commit_hash" \
  --build-arg BALANCE_FORK_PROFILE=Test \
  --tag nethermind:1.29.1-test \
  .

docker images 'nethermind:1.29.1*' --format 'table {{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.Size}}'
echo "next: ./scripts/test-balance-hardfork.sh"
