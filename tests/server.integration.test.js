import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { setTimeout as delay } from "node:timers/promises";
import { DatabaseSync } from "node:sqlite";
import { openDatabase } from "../server/database.js";
import { createBackendServer } from "../server/http-server.js";

async function startBackend(dbPath, options = {}) {
  const backend = createBackendServer({
    dbPath,
    allowDevelopmentEndpoints: true,
    turnMs: options.turnMs ?? 500,
    rng: options.rng ?? (() => 0.99)
  });
  const address = await backend.listen();
  return { backend, baseUrl: `http://127.0.0.1:${address.port}` };
}

async function request(baseUrl, path, { token, method = "GET", body, headers = {} } = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      ...(body ? { "content-type": "application/json" } : {}),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
      ...headers
    },
    body: body ? JSON.stringify(body) : undefined
  });
  const json = await response.json();
  return { status: response.status, body: json };
}

async function createAccount(baseUrl, dragonName, level = 1) {
  const result = await request(baseUrl, "/api/v1/accounts/guest", {
    method: "POST",
    body: {
      dragonName,
      ownerPetName: `${dragonName}주인`,
      species: dragonName === "어른용" ? "triceratops" : "tyrannosaurus",
      developmentStats: { level, attack: 0, evasion: 0, vitality: 0 }
    }
  });
  assert.equal(result.status, 201);
  return result.body;
}

async function createRegularAccount(baseUrl, dragonName) {
  const result = await request(baseUrl, "/api/v1/accounts/guest", {
    method: "POST",
    body: {
      dragonName,
      ownerPetName: `${dragonName}주인`,
      species: "tyrannosaurus"
    }
  });
  assert.equal(result.status, 201);
  return result.body;
}

async function rewardSteps(baseUrl, token, steps, key) {
  return request(baseUrl, "/api/v1/dev/steps", {
    method: "POST",
    token,
    headers: { "idempotency-key": key },
    body: { steps }
  });
}

async function choose(baseUrl, token, battleId, direction) {
  return request(baseUrl, `/api/v1/battles/${battleId}/choice`, {
    method: "POST",
    token,
    body: { direction }
  });
}

test("server-authoritative remote battle supports mixed stages and settles the burned pot", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigi-dragon-test-"));
  const dbPath = join(directory, "test.sqlite");
  let running = await startBackend(dbPath);

  try {
    const child = await createAccount(running.baseUrl, "아기용", 1);
    const adult = await createAccount(running.baseUrl, "어른용", 25);

    const firstReward = await rewardSteps(running.baseUrl, child.token, 1000, "steps-child-1");
    const duplicateReward = await rewardSteps(running.baseUrl, child.token, 1000, "steps-child-1");
    await rewardSteps(running.baseUrl, adult.token, 1000, "steps-adult-1");
    assert.equal(firstReward.body.balance, 1000);
    assert.equal(duplicateReward.body.applied, false);
    assert.equal(duplicateReward.body.balance, 1000);

    const queued = await request(running.baseUrl, "/api/v1/matchmaking", {
      method: "POST",
      token: child.token,
      body: { mode: "REMOTE", stake: 100 }
    });
    assert.equal(queued.status, 202);
    assert.equal(queued.body.status, "QUEUED");

    const matched = await request(running.baseUrl, "/api/v1/matchmaking", {
      method: "POST",
      token: adult.token,
      body: { mode: "REMOTE", stake: 100 }
    });
    assert.equal(matched.status, 201);
    assert.equal(matched.body.battle.players.A.dragon.stage, "GROWTH");
    assert.equal(matched.body.battle.players.B.dragon.stage, "ADULT");
    assert.equal(matched.body.battle.players.A.dragon.ownerPetName, undefined);
    const battleId = matched.body.battleId;

    for (let round = 1; round <= 5; round += 1) {
      const attackerIsChild = round % 2 === 1;
      const childDirection = attackerIsChild ? "LEFT" : "RIGHT";
      const adultDirection = "LEFT";
      assert.equal((await choose(running.baseUrl, child.token, battleId, childDirection)).status, 200);
      assert.equal((await choose(running.baseUrl, adult.token, battleId, adultDirection)).status, 200);
    }

    const finalBattle = await request(running.baseUrl, `/api/v1/battles/${battleId}`, { token: child.token });
    assert.equal(finalBattle.body.status, "FINISHED");
    assert.equal(finalBattle.body.winner, "A");
    assert.equal(finalBattle.body.payout, 160);
    assert.equal(finalBattle.body.burned, 40);

    const childMe = await request(running.baseUrl, "/api/v1/me", { token: child.token });
    const adultMe = await request(running.baseUrl, "/api/v1/me", { token: adult.token });
    assert.equal(childMe.body.wallet, 1060);
    assert.equal(adultMe.body.wallet, 900);

    await running.backend.close();
    running = await startBackend(dbPath);
    const persisted = await request(running.baseUrl, "/api/v1/me", { token: child.token });
    assert.equal(persisted.status, 200);
    assert.equal(persisted.body.wallet, 1060);
  } finally {
    await running.backend.close().catch(() => {});
    rmSync(directory, { recursive: true, force: true });
  }
});

test("missing choices become automatic after the turn deadline and paid remote cap is enforced", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigi-dragon-timeout-"));
  const dbPath = join(directory, "test.sqlite");
  const { backend, baseUrl } = await startBackend(dbPath, { turnMs: 80 });

  try {
    const a = await createAccount(baseUrl, "자동용A");
    const b = await createAccount(baseUrl, "자동용B");
    await rewardSteps(baseUrl, a.token, 1000, "auto-a");
    await rewardSteps(baseUrl, b.token, 1000, "auto-b");

    await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: a.token, body: { mode: "REMOTE", stake: 0 }
    });
    const match = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: b.token, body: { mode: "REMOTE", stake: 0 }
    });
    const battleId = match.body.battleId;
    await delay(105);
    const afterTimeout = await request(baseUrl, `/api/v1/battles/${battleId}`, { token: a.token });
    assert.ok(afterTimeout.body.history.length >= 1);
    assert.equal(afterTimeout.body.history[0].attackAutomatic, true);
    assert.equal(afterTimeout.body.history[0].defendAutomatic, true);

    const cappedUser = await createAccount(baseUrl, "제한용");
    await rewardSteps(baseUrl, cappedUser.token, 1000, "capped-user");
    const today = backend.wallets.dailyCounter(cappedUser.user.id).day;
    backend.db.prepare(`
      INSERT INTO daily_counters(user_id, day_kst, rewarded_steps, remote_paid_battles)
      VALUES (?, ?, 0, 20)
      ON CONFLICT(user_id, day_kst) DO UPDATE SET remote_paid_battles = 20
    `).run(cappedUser.user.id, today);
    const capped = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: cappedUser.token, body: { mode: "REMOTE", stake: 100 }
    });
    assert.equal(capped.status, 409);
    assert.match(capped.body.message, /daily battle limit/);
  } finally {
    await backend.close();
    rmSync(directory, { recursive: true, force: true });
  }
});

test("nearby wager matchmaking only joins peers with the same BLE pairing code", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-nearby-"));
  const dbPath = join(directory, "test.sqlite");
  const { backend, baseUrl } = await startBackend(dbPath);

  try {
    const host = await createAccount(baseUrl, "근처호스트");
    const guest = await createAccount(baseUrl, "근처게스트");
    const stranger = await createAccount(baseUrl, "다른방");
    await rewardSteps(baseUrl, host.token, 1000, "nearby-host");
    await rewardSteps(baseUrl, guest.token, 1000, "nearby-guest");
    await rewardSteps(baseUrl, stranger.token, 1000, "nearby-stranger");

    const hostQueue = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: host.token,
      body: { mode: "NEARBY", stake: 1000, pairingCode: "ROOM-A1" }
    });
    assert.equal(hostQueue.status, 202);

    const strangerQueue = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: stranger.token,
      body: { mode: "NEARBY", stake: 1000, pairingCode: "ROOM-B2" }
    });
    assert.equal(strangerQueue.status, 202);

    const match = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: guest.token,
      body: { mode: "NEARBY", stake: 1000, pairingCode: "room-a1" }
    });
    assert.equal(match.status, 201);
    assert.equal(match.body.battle.mode, "NEARBY");
    assert.equal(match.body.battle.stake, 1000);
    assert.equal(match.body.battle.players.A.userId, host.user.id);
    assert.ok(Number.isInteger(match.body.battle.serverNow));
    assert.ok(match.body.battle.deadlineAt >= match.body.battle.serverNow);

    const published = await request(baseUrl, `/api/v1/battles/${match.body.battleId}/anchor`, {
      method: "POST", token: host.token, body: { cloudAnchorId: "anchor_demo_123" }
    });
    assert.equal(published.status, 200);
    const guestView = await request(baseUrl, `/api/v1/battles/${match.body.battleId}`, { token: guest.token });
    assert.equal(guestView.body.cloudAnchorId, "anchor_demo_123");
    const guestCannotReplace = await request(baseUrl, `/api/v1/battles/${match.body.battleId}/anchor`, {
      method: "POST", token: guest.token, body: { cloudAnchorId: "anchor_attack_999" }
    });
    assert.equal(guestCannotReplace.status, 400);

    const stillWaiting = await request(baseUrl, "/api/v1/matchmaking", { token: stranger.token });
    assert.equal(stillWaiting.body.status, "QUEUED");
    assert.equal(stillWaiting.body.pairingCode, "ROOM-B2");
  } finally {
    await backend.close();
    rmSync(directory, { recursive: true, force: true });
  }
});

test("Health Connect cumulative totals reward only the new daily step delta", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigi-dragon-health-"));
  const dbPath = join(directory, "test.sqlite");
  const { backend, baseUrl } = await startBackend(dbPath);

  try {
    const account = await createAccount(baseUrl, "걷는용");
    const day = backend.wallets.dailyCounter(account.user.id).day;
    const sync = (totalSteps, key) => request(baseUrl, "/api/v1/steps/sync", {
      method: "POST",
      token: account.token,
      headers: { "idempotency-key": key },
      body: { day, totalSteps }
    });

    const first = await sync(1000, `hc:${day}:1000`);
    const duplicate = await sync(1000, `hc:${day}:1000`);
    const increased = await sync(1500, `hc:${day}:1500`);
    const lowerReading = await sync(1400, `hc:${day}:1400`);
    const capped = await sync(20_000, `hc:${day}:20000`);

    assert.equal(first.body.rewarded, 1000);
    assert.equal(duplicate.body.duplicate, true);
    assert.equal(increased.body.rewarded, 500);
    assert.equal(lowerReading.body.rewarded, 0);
    assert.equal(capped.body.rewarded, 8500);
    assert.equal(capped.body.balance, 10_000);

    const transactions = await request(baseUrl, "/api/v1/wallet/transactions", { token: account.token });
    assert.equal(transactions.body.balance, 10_000);
    assert.equal(
      transactions.body.transactions.filter((item) => item.type === "HEALTH_CONNECT_STEP_REWARD").length,
      3
    );
  } finally {
    await backend.close();
    rmSync(directory, { recursive: true, force: true });
  }
});

test("starter gift, egg progress, cleaning, and equal-pool hatch are server authoritative", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigi-dragon-journey-"));
  const dbPath = join(directory, "test.sqlite");
  const { backend, baseUrl } = await startBackend(dbPath, { rng: () => 0.99 });

  try {
    const account = await createRegularAccount(baseUrl, "별빛용");
    assert.equal(account.user.journey.phase, "GIFT");
    assert.equal(account.user.dragon, null);

    const prematureBattle = await request(baseUrl, "/api/v1/matchmaking", {
      method: "POST", token: account.token, body: { mode: "REMOTE", stake: 0 }
    });
    assert.equal(prematureBattle.status, 409);

    const opened = await request(baseUrl, "/api/v1/journey/gift/open", {
      method: "POST", token: account.token
    });
    assert.equal(opened.body.journey.phase, "EGG");
    assert.equal(opened.body.journey.egg.progress, 0);

    const day = backend.wallets.dailyCounter(account.user.id).day;
    const sync = (totalSteps, key) => request(baseUrl, "/api/v1/steps/sync", {
      method: "POST",
      token: account.token,
      headers: { "idempotency-key": key },
      body: { day, totalSteps }
    });
    const walked = await sync(29_000, `egg:${day}:29000`);
    assert.equal(walked.body.eggProgressAdded, 29_000);
    assert.equal(walked.body.rewarded, 0);
    assert.equal(walked.body.balance, 0);

    const cleaned = await request(baseUrl, "/api/v1/journey/egg/clean", {
      method: "POST", token: account.token
    });
    assert.equal(cleaned.body.journey.egg.progress, 29_500);
    const cooldown = await request(baseUrl, "/api/v1/journey/egg/clean", {
      method: "POST", token: account.token
    });
    assert.equal(cooldown.status, 409);

    const ready = await sync(29_500, `egg:${day}:29500`);
    assert.equal(ready.body.journey.phase, "READY_TO_HATCH");
    assert.equal(ready.body.journey.egg.progress, 30_000);

    const hatched = await request(baseUrl, "/api/v1/journey/egg/hatch", {
      method: "POST", token: account.token
    });
    assert.equal(hatched.body.journey.phase, "HATCHED");
    assert.equal(hatched.body.dragon.species, "titanosaurus");
    assert.equal(hatched.body.dragon.artId, 30);
    assert.equal(hatched.body.dragon.eyeColor.key, "silver");
    assert.equal(hatched.body.dragon.wingColor.key, "obsidian");

    const afterHatch = await sync(30_000, `home:${day}:30000`);
    assert.equal(afterHatch.body.eggProgressAdded, 0);
    assert.equal(afterHatch.body.rewarded, 500);
    assert.equal(afterHatch.body.balance, 500);
  } finally {
    await backend.close();
    rmSync(directory, { recursive: true, force: true });
  }
});

test("v0.4 databases migrate existing dragons to a hatched journey", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigi-dragon-migration-"));
  const dbPath = join(directory, "old.sqlite");
  const old = new DatabaseSync(dbPath);
  old.exec(`
    CREATE TABLE users (
      id TEXT PRIMARY KEY, token_hash TEXT NOT NULL UNIQUE,
      public_dragon_name TEXT NOT NULL, owner_pet_name TEXT NOT NULL, created_at INTEGER NOT NULL
    );
    CREATE TABLE dragons (
      id TEXT PRIMARY KEY, user_id TEXT NOT NULL UNIQUE REFERENCES users(id), species TEXT NOT NULL,
      level INTEGER NOT NULL, experience INTEGER NOT NULL, attack INTEGER NOT NULL,
      evasion INTEGER NOT NULL, vitality INTEGER NOT NULL, created_at INTEGER NOT NULL
    );
    CREATE TABLE wallets (user_id TEXT PRIMARY KEY REFERENCES users(id), balance INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE step_sync_requests (
      user_id TEXT NOT NULL, idempotency_key TEXT NOT NULL, day_kst TEXT NOT NULL,
      reported_total_steps INTEGER NOT NULL, rewarded_steps INTEGER NOT NULL, created_at INTEGER NOT NULL,
      PRIMARY KEY(user_id, idempotency_key)
    );
    INSERT INTO users VALUES ('legacy-user', 'hash', '기존용', '주인님', 1000);
    INSERT INTO dragons VALUES ('legacy-dragon', 'legacy-user', 'tyrannosaurus', 5, 0, 1, 2, 3, 1000);
    INSERT INTO wallets VALUES ('legacy-user', 1234);
  `);
  old.close();

  try {
    const migrated = openDatabase(dbPath);
    const columns = migrated.prepare("PRAGMA table_info(step_sync_requests)").all();
    const journey = migrated.prepare("SELECT phase, egg_progress FROM journeys WHERE user_id = ?").get("legacy-user");
    assert.ok(columns.some((column) => column.name === "egg_progress_added"));
    assert.equal(journey.phase, "HATCHED");
    assert.equal(journey.egg_progress, 30_000);
    migrated.close();
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
