#!/usr/bin/env bash
# JSON-era miner (nethermind:1.29.1-old) vs hardcoded test binary (nethermind:1.29.1-test).
# Fork block N=20. After miner passes N, check:
#   1) sync from genesis
#   2) FastSync from N-10
# State root at N and at head must match the miner.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CFG="$ROOT/templates/test"
NET="hf-test-net"
MINER="hf-old-miner"
SYNC_GENESIS="hf-test-genesis"
SYNC_PIVOT="hf-test-pivot"
FORK=20
PIVOT=$((FORK - 10))
FROM="0x685ae1620e55cd292d31c1215374908b4f5e2555"
TO1="0x3908e868b0b2abec816c4b9b494568decbbd08d2"
TO2="0x000000000000000000000000000000000000dead"
RPC() {
  local url="$1" method="$2" params="${3:-[]}"
  curl -sf -H 'content-type: application/json' \
    --data "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"$method\",\"params\":$params}" \
    "$url"
}

hex_to_dec() {
  python3 -c "print(int('$1', 16))"
}

block_number() {
  local hex
  hex="$(RPC "$1" eth_blockNumber 2>/dev/null | python3 -c 'import json,sys; print(json.load(sys.stdin)["result"])' 2>/dev/null || true)"
  if [ -z "$hex" ] || [ "$hex" = "None" ]; then
    echo 0
    return 0
  fi
  hex_to_dec "$hex"
}

block_field() {
  local url="$1" num="$2" field="$3"
  RPC "$url" eth_getBlockByNumber "[\"$(printf '0x%x' "$num")\", false]" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['result']['$field'])"
}

balance() {
  RPC "$1" eth_getBalance "[\"$2\", \"$(printf '0x%x' "$3")\"]" \
    | python3 -c 'import json,sys; print(int(json.load(sys.stdin)["result"], 16))'
}

wait_block() {
  local url="$1" target="$2"
  echo "waiting for $url to reach block $target"
  for _ in $(seq 1 180); do
    local n
    n="$(block_number "$url" || echo 0)"
    echo "  head=$n"
    if [ "$n" -ge "$target" ]; then
      return 0
    fi
    sleep 2
  done
  echo "timeout waiting for block $target" >&2
  return 1
}

cleanup() {
  docker rm -f "$MINER" "$SYNC_GENESIS" "$SYNC_PIVOT" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
}

cleanup
docker network create "$NET"

echo "== miner 1.29.1-old + JSON =="
docker run -d --name "$MINER" --network "$NET" \
  -p 8545:8545 \
  -v "$CFG:/config:ro" \
  nethermind:1.29.1-old \
  --config /config/all-old.cfg

wait_block http://127.0.0.1:8545 $((FORK + 5))

ROOT_FORK="$(block_field http://127.0.0.1:8545 "$FORK" stateRoot)"
HEAD="$(block_number http://127.0.0.1:8545)"
ROOT_HEAD="$(block_field http://127.0.0.1:8545 "$HEAD" stateRoot)"
PIVOT_HASH="$(block_field http://127.0.0.1:8545 "$PIVOT" hash)"
PIVOT_TD="$(block_field http://127.0.0.1:8545 "$PIVOT" totalDifficulty)"
BAL_FROM="$(balance http://127.0.0.1:8545 "$FROM" "$FORK")"
BAL_TO1="$(balance http://127.0.0.1:8545 "$TO1" "$FORK")"
BAL_TO2="$(balance http://127.0.0.1:8545 "$TO2" "$FORK")"

echo "miner head=$HEAD"
echo "stateRoot@$FORK=$ROOT_FORK"
echo "stateRoot@$HEAD=$ROOT_HEAD"
echo "pivot $PIVOT hash=$PIVOT_HASH td=$PIVOT_TD"
echo "balances@$FORK from=$BAL_FROM to1=$BAL_TO1 to2=$BAL_TO2"

if [ "$BAL_TO1" = "0" ] || [ "$BAL_TO2" = "0" ]; then
  echo "FAIL: JSON fork did not move balances at block $FORK" >&2
  docker logs "$MINER" | tail -80
  exit 1
fi

ENODE="$(RPC http://127.0.0.1:8545 admin_nodeInfo | python3 -c 'import json,sys,re; e=json.load(sys.stdin)["result"]["enode"]; print(re.sub(r"@[^:]+", "@'"$MINER"'", e, count=1))')"
echo "enode=$ENODE"

echo "== 1.29.1-test sync from genesis =="
docker run -d --name "$SYNC_GENESIS" --network "$NET" \
  -p 8555:8545 \
  -v "$CFG:/config:ro" \
  nethermind:1.29.1-test \
  --config /config/all-test.cfg \
  --Init.BaseDbPath nethermind_db/hf-genesis \
  --Network.P2PPort 30301 \
  --Network.DiscoveryPort 30301 \
  --Network.StaticPeers "$ENODE"

wait_block http://127.0.0.1:8555 "$HEAD"
GEN_FORK="$(block_field http://127.0.0.1:8555 "$FORK" stateRoot)"
GEN_HEAD="$(block_field http://127.0.0.1:8555 "$HEAD" stateRoot)"
echo "genesis-sync stateRoot@$FORK=$GEN_FORK"
echo "genesis-sync stateRoot@$HEAD=$GEN_HEAD"
if [ "$GEN_FORK" != "$ROOT_FORK" ] || [ "$GEN_HEAD" != "$ROOT_HEAD" ]; then
  echo "FAIL: genesis sync state root mismatch" >&2
  exit 1
fi
echo "OK genesis sync"

echo "== 1.29.1-test FastSync from N-10 ($PIVOT) =="
docker run -d --name "$SYNC_PIVOT" --network "$NET" \
  -p 8565:8545 \
  -v "$CFG:/config:ro" \
  nethermind:1.29.1-test \
  --config /config/all-test.cfg \
  --Init.BaseDbPath nethermind_db/hf-pivot \
  --Network.P2PPort 30302 \
  --Network.DiscoveryPort 30302 \
  --Network.StaticPeers "$ENODE" \
  --Sync.FastSync true \
  --Sync.PivotNumber "$PIVOT" \
  --Sync.PivotHash "$PIVOT_HASH" \
  --Sync.PivotTotalDifficulty "$PIVOT_TD" \
  --Sync.UseGethLimitsInFastBlocks false

wait_block http://127.0.0.1:8565 "$HEAD"
PIV_FORK="$(block_field http://127.0.0.1:8565 "$FORK" stateRoot)"
PIV_HEAD="$(block_field http://127.0.0.1:8565 "$HEAD" stateRoot)"
echo "pivot-sync stateRoot@$FORK=$PIV_FORK"
echo "pivot-sync stateRoot@$HEAD=$PIV_HEAD"
if [ "$PIV_FORK" != "$ROOT_FORK" ] || [ "$PIV_HEAD" != "$ROOT_HEAD" ]; then
  echo "FAIL: n-10 FastSync state root mismatch" >&2
  exit 1
fi
echo "OK n-10 FastSync"

echo
echo "all checks passed: JSON miner and hardcoded 1.29.1-test share state roots"
echo "live image nethermind:1.29.1 still uses block 36333941 (do not use it on this test net)"
cleanup
