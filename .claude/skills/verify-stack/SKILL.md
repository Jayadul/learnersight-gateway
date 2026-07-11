---
name: verify-stack
description: Smoke-test the running NetZero Gateway docker stack - containers, login, gateway round-trip, role restrictions. Use after deployments or when the user asks to verify/check the stack.
---

# Verify Stack

Full reference: `learnersight-netzero-gateway/docs/testing.md`. Steps:

1. `docker compose ps` (workspace root) — 5 services up, db healthy.
2. Admin API login + a protected endpoint:
   ```powershell
   $l = Invoke-RestMethod -Method Post -Uri "http://localhost:18081/api/auth/login" `
     -ContentType "application/json" -Body '{"username":"admin","password":"Admin@123"}'
   Invoke-RestMethod -Uri "http://localhost:18081/api/dashboard/summary" `
     -Headers @{ Authorization = "Bearer $($l.accessToken)" }
   ```
3. Gateway negative test (no creds → must be `101 REJECTD`):
   ```powershell
   Invoke-RestMethod -Method Post -Uri "http://localhost:18081/sendtext" `
     -ContentType "application/x-www-form-urlencoded" -Body "toUser=8801700000001&messageContent=test"
   ```
   Positive test needs the apikey/secretkey from the active SMS channel config
   (decrypt via admin API or ask the user) — only send real messages if the user okays it.
4. Role check: login as `operator`/`Operator@123` → GET `/api/channel-configs` must 403.
5. Web reachable: `Invoke-WebRequest -UseBasicParsing http://localhost:18080` → 200.
6. DB direct (optional): `docker exec netzero-gateway-db-1 psql -U postgres -d NetZeroGatewayDB -c "select count(*) from message_requests;"`

Report as a short pass/fail table; on failure include the exact response and the
container logs excerpt (`docker logs netzero-gateway-api-1 --tail 30`).

Always use `-UseBasicParsing` with Invoke-WebRequest (NonInteractive shell).
