import http from "node:http";
import { mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { AccountService } from "./account-service.js";
import { BattleService } from "./battle-service.js";
import { openDatabase } from "./database.js";
import { WalletService } from "./wallet-service.js";
import { JourneyService } from "./journey-service.js";

const MAX_BODY_BYTES = 64 * 1024;

function sendJson(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "content-length": Buffer.byteLength(body),
    "cache-control": "no-store"
  });
  response.end(body);
}

async function readJson(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > MAX_BODY_BYTES) throw new Error("request body too large");
    chunks.push(chunk);
  }
  if (chunks.length === 0) return {};
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    throw new Error("invalid JSON body");
  }
}

function bearerToken(request) {
  const value = request.headers.authorization ?? "";
  return value.startsWith("Bearer ") ? value.slice(7).trim() : null;
}

function errorStatus(message) {
  if (message.includes("access denied") || message.includes("not found")) return 404;
  if (
    message.includes("insufficient") ||
    message.includes("already") ||
    message.includes("limit reached") ||
    message.includes("deadline") ||
    message.includes("not active") ||
    message.includes("cooldown") ||
    message.includes("unavailable") ||
    message.includes("not ready") ||
    message.includes("not hatched")
  ) return 409;
  return 400;
}

export function createBackendServer({
  dbPath = ":memory:",
  allowDevelopmentEndpoints = false,
  turnMs,
  rng,
  now
} = {}) {
  if (dbPath !== ":memory:") mkdirSync(dirname(dbPath), { recursive: true });
  const db = openDatabase(dbPath);
  const accounts = new AccountService(db);
  const wallets = new WalletService(db);
  const journeys = new JourneyService(db, accounts, { rng, now });
  const battles = new BattleService(db, accounts, wallets, { turnMs, rng, now });
  const streams = new Set();

  battles.on("error", () => {});

  const server = http.createServer(async (request, response) => {
    response.setHeader("access-control-allow-origin", "*");
    response.setHeader("access-control-allow-headers", "authorization, content-type, idempotency-key");
    response.setHeader("access-control-allow-methods", "GET, POST, DELETE, OPTIONS");
    if (request.method === "OPTIONS") {
      response.writeHead(204);
      response.end();
      return;
    }

    const url = new URL(request.url, "http://localhost");
    try {
      if (request.method === "GET" && url.pathname === "/api/v1/health") {
        sendJson(response, 200, { ok: true, service: "bigimong", version: "0.10.0" });
        return;
      }

      if (request.method === "POST" && url.pathname === "/api/v1/accounts/guest") {
        const body = await readJson(request);
        if (!allowDevelopmentEndpoints && body.developmentStats) {
          throw new Error("developmentStats is disabled");
        }
        const result = accounts.createGuest({
          dragonName: body.dragonName,
          ownerPetName: body.ownerPetName,
          species: body.species,
          developmentStats: allowDevelopmentEndpoints ? body.developmentStats : null
        });
        sendJson(response, 201, result);
        return;
      }

      const user = accounts.authenticate(bearerToken(request));
      if (!user) {
        sendJson(response, 401, { error: "UNAUTHORIZED", message: "valid bearer token required" });
        return;
      }

      if (request.method === "GET" && url.pathname === "/api/v1/me") {
        sendJson(response, 200, accounts.getById(user.id));
        return;
      }

      if (request.method === "POST" && url.pathname === "/api/v1/journey/gift/open") {
        sendJson(response, 200, journeys.openGift(user.id));
        return;
      }

      if (request.method === "POST" && url.pathname === "/api/v1/journey/egg/clean") {
        sendJson(response, 200, journeys.cleanEgg(user.id));
        return;
      }

      if (request.method === "POST" && url.pathname === "/api/v1/journey/egg/hatch") {
        sendJson(response, 200, journeys.hatch(user.id));
        return;
      }

      if (url.pathname === "/api/v1/dev/steps") {
        if (!allowDevelopmentEndpoints) {
          sendJson(response, 404, { error: "NOT_FOUND" });
          return;
        }
        if (request.method !== "POST") throw new Error("method not allowed");
        const body = await readJson(request);
        const idempotencyKey = request.headers["idempotency-key"] ?? body.idempotencyKey;
        const result = wallets.rewardDevelopmentSteps(user.id, body.steps, idempotencyKey);
        sendJson(response, 200, result);
        return;
      }

      if (request.method === "POST" && url.pathname === "/api/v1/steps/sync") {
        const body = await readJson(request);
        const idempotencyKey = request.headers["idempotency-key"] ?? body.idempotencyKey;
        const result = wallets.syncDailyCumulativeSteps(user.id, {
          day: body.day,
          totalSteps: body.totalSteps,
          idempotencyKey
        });
        sendJson(response, 200, result);
        return;
      }

      if (request.method === "GET" && url.pathname === "/api/v1/wallet/transactions") {
        const limit = Number(url.searchParams.get("limit") ?? 50);
        sendJson(response, 200, { balance: wallets.balance(user.id), transactions: wallets.list(user.id, limit) });
        return;
      }

      if (url.pathname === "/api/v1/matchmaking") {
        if (request.method === "POST") {
          const body = await readJson(request);
          const result = battles.joinQueue(user.id, {
            mode: body.mode,
            stake: body.stake,
            pairingCode: body.pairingCode
          });
          if (result.battleId) result.battle = battles.getBattle(result.battleId, user.id);
          sendJson(response, result.status === "MATCHED" ? 201 : 202, result);
          return;
        }
        if (request.method === "GET") {
          sendJson(response, 200, battles.queueStatus(user.id));
          return;
        }
        if (request.method === "DELETE") {
          sendJson(response, 200, battles.cancelQueue(user.id));
          return;
        }
      }

      const battleMatch = url.pathname.match(/^\/api\/v1\/battles\/([^/]+)$/);
      if (battleMatch && request.method === "GET") {
        sendJson(response, 200, battles.getBattle(battleMatch[1], user.id));
        return;
      }

      const choiceMatch = url.pathname.match(/^\/api\/v1\/battles\/([^/]+)\/choice$/);
      if (choiceMatch && request.method === "POST") {
        const body = await readJson(request);
        sendJson(response, 200, battles.submitChoice(choiceMatch[1], user.id, body.direction));
        return;
      }

      const anchorMatch = url.pathname.match(/^\/api\/v1\/battles\/([^/]+)\/anchor$/);
      if (anchorMatch && request.method === "POST") {
        const body = await readJson(request);
        sendJson(response, 200, battles.publishCloudAnchor(anchorMatch[1], user.id, body.cloudAnchorId));
        return;
      }

      const eventsMatch = url.pathname.match(/^\/api\/v1\/battles\/([^/]+)\/events$/);
      if (eventsMatch && request.method === "GET") {
        const battleId = eventsMatch[1];
        battles.getBattle(battleId, user.id);
        response.writeHead(200, {
          "content-type": "text/event-stream; charset=utf-8",
          "cache-control": "no-cache, no-transform",
          connection: "keep-alive"
        });
        response.write(`event: snapshot\ndata: ${JSON.stringify(battles.getBattle(battleId, user.id))}\n\n`);
        const onBattleEvent = (event) => {
          if (!response.destroyed) response.write(`event: battle\ndata: ${JSON.stringify(event)}\n\n`);
        };
        const heartbeat = setInterval(() => {
          if (!response.destroyed) response.write(": keepalive\n\n");
        }, 15_000);
        heartbeat.unref?.();
        battles.on(`battle:${battleId}`, onBattleEvent);
        streams.add(response);
        request.on("close", () => {
          clearInterval(heartbeat);
          battles.off(`battle:${battleId}`, onBattleEvent);
          streams.delete(response);
        });
        return;
      }

      sendJson(response, 404, { error: "NOT_FOUND" });
    } catch (error) {
      if (!response.headersSent) {
        sendJson(response, errorStatus(error.message), { error: "REQUEST_FAILED", message: error.message });
      } else {
        response.destroy();
      }
    }
  });

  return {
    server,
    db,
    accounts,
    wallets,
    journeys,
    battles,
    async listen(port = 0, host = "127.0.0.1") {
      await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(port, host, resolve);
      });
      return server.address();
    },
    async close() {
      for (const stream of streams) stream.destroy();
      streams.clear();
      battles.close();
      if (server.listening) await new Promise((resolve) => server.close(resolve));
      db.close();
    }
  };
}
