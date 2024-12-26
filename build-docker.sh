#!/bin/sh
commit_hash=$(git rev-parse HEAD)
echo $commit_hash
build_timestamp=$(date '+%s')
echo $commit_date

docker build \
  -f Dockerfile.prod \
  --build-arg COMMIT_HASH="${commit_hash}" \
  --build-arg BUILD_TIMESTAMP="${build_timestamp}" \
  --tag "nethermind:2.8.0" .