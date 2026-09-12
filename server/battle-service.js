import { EventEmitter } from "node:events";
import { randomUUID } from "node:crypto";
import {
  DIRECTIONS,
  RULES,
  createBattle,
  resolveRound,
  validateBattleEntry
} from "../src/game-engine.js";
import { inTransaction, kstDay } from "./database.js";

function publicDragon(dragon) {
  return {
    id: dragon.id,
    name: dragon.name,
    species: dragon.species,
    level: dragon.level,
    stage: dragon.stage,
    experience: dragon.experience,
    artId: dragon.artId,
    stats: structuredClone(dragon.stats)
  };
}

function parseBattle(row) {
  if (!row) return null;
  return {
    row,
    state: JSON.parse(row.state_json)
  };
}

function normalizePairingCode(mode, value) {
  if (mode !== "NEARBY") return null;
  const code = String(value ?? "").trim().toUpperCase();
  if (!/^[A-Z0-9-]{6,32}$/.test(code)) throw new Error("invalid nearby pairing code");
  return code;
}

export class BattleService extends EventEmitter {
  constructor(db, accountService, walletService, {
    turnMs = RULES.turnSeconds * 1000,
    rng = Math.random,
    now = Date.now
  } = {}) {
    super();
    this.db = db;
    this.accounts = accountService;
    this.wallets = walletService;
    this.turnMs = turnMs;
    this.rng = rng;
    this.now = now;
    this.timers = new Map();
    this.recover();
  }

  joinQueue(userId, { mode, stake, pairingCode }, nowMs = this.now()) {
    const callerAccount = this.accounts.getById(userId);
    if (!callerAccount?.dragon) throw new Error("dragon has not hatched");
    if (!RULES.battleModes[mode]) throw new Error("unknown battle mode");
    if (!Number.isInteger(stake) || stake < 0) throw new Error("invalid stake");
    if (mode === "NEARBY" && stake !== RULES.battleModes.NEARBY.maxStake) {
      throw new Error("nearby battle stake must be 1000 bigi");
    }
    const normalizedPairingCode = normalizePairingCode(mode, pairingCode);

    const result = inTransaction(this.db, () => {
      const active = this.db.prepare(`
        SELECT bu.battle_id AS battleId
        FROM battle_users bu
        JOIN battles b ON b.id = bu.battle_id
        WHERE bu.user_id = ? AND b.status = 'ACTIVE'
        LIMIT 1
      `).get(userId);
      if (active) return { status: "MATCHED", battleId: active.battleId };

      const existing = this.db.prepare(`
        SELECT id, mode, stake, pairing_code AS pairingCode, created_at AS createdAt
        FROM battle_queue WHERE user_id = ?
      `).get(userId);
      if (existing) {
        if (existing.mode !== mode || existing.stake !== stake || existing.pairingCode !== normalizedPairingCode) {
          throw new Error("already queued with different battle settings");
        }
        return { status: "QUEUED", queueId: existing.id, mode, stake };
      }

      const callerCounter = this.wallets.dailyCounter(userId, nowMs);
      validateBattleEntry({ mode, stake, remoteBattlesToday: callerCounter.remotePaidBattles });
      if (this.wallets.balance(userId) < stake) throw new Error("insufficient battle stake");

      const opponent = this.db.prepare(`
        SELECT id, user_id AS userId, created_at AS createdAt
        FROM battle_queue
        WHERE mode = ? AND stake = ? AND user_id <> ?
          AND (? IS NULL OR pairing_code = ?)
        ORDER BY created_at ASC
        LIMIT 1
      `).get(mode, stake, userId, normalizedPairingCode, normalizedPairingCode);

      if (!opponent) {
        const queueId = randomUUID();
        this.db.prepare(`
          INSERT INTO battle_queue(id, user_id, mode, stake, pairing_code, created_at)
          VALUES (?, ?, ?, ?, ?, ?)
        `).run(queueId, userId, mode, stake, normalizedPairingCode, nowMs);
        return { status: "QUEUED", queueId, mode, stake };
      }

      const opponentCounter = this.wallets.dailyCounter(opponent.userId, nowMs);
      validateBattleEntry({ mode, stake, remoteBattlesToday: opponentCounter.remotePaidBattles });
      if (this.wallets.balance(opponent.userId) < stake) {
        this.db.prepare("DELETE FROM battle_queue WHERE id = ?").run(opponent.id);
        throw new Error("matched opponent no longer has enough bigi");
      }

      const accountA = this.accounts.getById(opponent.userId);
      const accountB = this.accounts.getById(userId);
      if (!accountA || !accountB) throw new Error("battle account not found");

      const battleId = randomUUID();
      const deadlineAt = nowMs + this.turnMs;
      const state = createBattle({
        playerA: { userId: accountA.id, wallet: accountA.wallet, dragon: publicDragon(accountA.dragon) },
        playerB: { userId: accountB.id, wallet: accountB.wallet, dragon: publicDragon(accountB.dragon) },
        mode,
        stake,
        remoteBattlesTodayA: opponentCounter.remotePaidBattles,
        remoteBattlesTodayB: callerCounter.remotePaidBattles
      });
      state.id = battleId;
      state.choices = { A: null, B: null };
      state.settled = false;

      if (stake > 0) {
        this.wallets.applyInsideTransaction(accountA.id, {
          amount: -stake,
          type: "BATTLE_STAKE",
          referenceId: battleId,
          idempotencyKey: `battle:${battleId}:stake`,
          nowMs
        });
        this.wallets.applyInsideTransaction(accountB.id, {
          amount: -stake,
          type: "BATTLE_STAKE",
          referenceId: battleId,
          idempotencyKey: `battle:${battleId}:stake`,
          nowMs
        });
      }

      if (mode === "REMOTE" && stake > 0) {
        this.incrementRemoteBattle(accountA.id, nowMs);
        this.incrementRemoteBattle(accountB.id, nowMs);
      }

      this.db.prepare(`
        INSERT INTO battles(id, mode, stake, status, state_json, deadline_at, created_at, updated_at)
        VALUES (?, ?, ?, 'ACTIVE', ?, ?, ?, ?)
      `).run(battleId, mode, stake, JSON.stringify(state), deadlineAt, nowMs, nowMs);
      this.db.prepare(`INSERT INTO battle_users(battle_id, user_id, side) VALUES (?, ?, 'A')`)
        .run(battleId, accountA.id);
      this.db.prepare(`INSERT INTO battle_users(battle_id, user_id, side) VALUES (?, ?, 'B')`)
        .run(battleId, accountB.id);
      this.db.prepare("DELETE FROM battle_queue WHERE id = ?").run(opponent.id);

      return { status: "MATCHED", battleId };
    });

    if (result.battleId) this.schedule(result.battleId);
    return result;
  }

  incrementRemoteBattle(userId, nowMs) {
    const day = kstDay(nowMs);
    this.db.prepare(`
      INSERT INTO daily_counters(user_id, day_kst, rewarded_steps, remote_paid_battles)
      VALUES (?, ?, 0, 1)
      ON CONFLICT(user_id, day_kst)
      DO UPDATE SET remote_paid_battles = remote_paid_battles + 1
    `).run(userId, day);
  }

  queueStatus(userId, nowMs = this.now()) {
    const active = this.db.prepare(`
      SELECT bu.battle_id AS battleId
      FROM battle_users bu JOIN battles b ON b.id = bu.battle_id
      WHERE bu.user_id = ? AND b.status = 'ACTIVE'
      ORDER BY b.created_at DESC LIMIT 1
    `).get(userId);
    if (active) return { status: "MATCHED", battle: this.getBattle(active.battleId, userId, nowMs) };
    const queued = this.db.prepare(`
      SELECT id AS queueId, mode, stake, pairing_code AS pairingCode, created_at AS createdAt
      FROM battle_queue WHERE user_id = ?
    `).get(userId);
    return queued ? { status: "QUEUED", ...queued } : { status: "IDLE" };
  }

  cancelQueue(userId) {
    const result = this.db.prepare("DELETE FROM battle_queue WHERE user_id = ?").run(userId);
    return { cancelled: result.changes > 0 };
  }

  getBattle(battleId, userId, nowMs = this.now()) {
    this.assertParticipant(battleId, userId);
    this.resolveExpired(battleId, nowMs);
    const battle = parseBattle(this.db.prepare("SELECT * FROM battles WHERE id = ?").get(battleId));
    if (!battle) throw new Error("battle not found");
    return this.clientView(battle, userId, nowMs);
  }

  submitChoice(battleId, userId, direction, nowMs = this.now()) {
    if (!DIRECTIONS.includes(direction)) throw new Error("invalid direction");
    const side = this.assertParticipant(battleId, userId);
    let parsed = parseBattle(this.db.prepare("SELECT * FROM battles WHERE id = ?").get(battleId));
    if (!parsed) throw new Error("battle not found");
    if (parsed.state.status !== "ACTIVE") throw new Error("battle is not active");
    if (nowMs >= parsed.row.deadline_at) {
      this.resolveExpired(battleId, nowMs);
      throw new Error("turn deadline passed; automatic play was used");
    }
    if (parsed.state.choices[side]) throw new Error("choice already submitted for this turn");

    parsed.state.choices[side] = direction;
    this.db.prepare(`
      UPDATE battles SET state_json = ?, updated_at = ? WHERE id = ?
    `).run(JSON.stringify(parsed.state), nowMs, battleId);

    let event = null;
    if (parsed.state.choices.A && parsed.state.choices.B) {
      event = this.resolveCurrentRound(battleId, nowMs);
    }
    return { accepted: true, event, battle: this.getBattle(battleId, userId, nowMs) };
  }

  resolveExpired(battleId, nowMs = this.now()) {
    const row = this.db.prepare("SELECT status, deadline_at FROM battles WHERE id = ?").get(battleId);
    if (!row || row.status !== "ACTIVE" || nowMs < row.deadline_at) return null;
    return this.resolveCurrentRound(battleId, nowMs);
  }

  resolveCurrentRound(battleId, nowMs = this.now()) {
    const parsed = parseBattle(this.db.prepare("SELECT * FROM battles WHERE id = ?").get(battleId));
    if (!parsed || parsed.state.status !== "ACTIVE") return null;
    const state = parsed.state;
    const attacker = state.attacker;
    const defender = attacker === "A" ? "B" : "A";
    const result = resolveRound(state, {
      attackDirection: state.choices[attacker] ?? undefined,
      defendDirection: state.choices[defender] ?? undefined
    }, this.rng);
    result.battle.choices = { A: null, B: null };
    const finished = result.battle.status === "FINISHED";
    const nextDeadline = finished ? nowMs : nowMs + this.turnMs;

    inTransaction(this.db, () => {
      if (finished && !result.battle.settled) {
        const winnerUserId = result.battle.players[result.battle.winner].userId;
        if (result.battle.payout > 0) {
          this.wallets.applyInsideTransaction(winnerUserId, {
            amount: result.battle.payout,
            type: "BATTLE_WIN",
            referenceId: battleId,
            idempotencyKey: `battle:${battleId}:payout`,
            nowMs
          });
        }
        result.battle.settled = true;
      }
      this.db.prepare(`
        UPDATE battles
        SET status = ?, state_json = ?, deadline_at = ?, updated_at = ?
        WHERE id = ?
      `).run(result.battle.status, JSON.stringify(result.battle), nextDeadline, nowMs, battleId);
    });

    if (finished) this.clearTimer(battleId);
    else this.schedule(battleId);
    this.emit(`battle:${battleId}`, { type: finished ? "BATTLE_FINISHED" : "ROUND_RESOLVED", event: result.event });
    return result.event;
  }

  clientView(parsed, userId, serverNow = this.now()) {
    const side = this.assertParticipant(parsed.state.id, userId);
    const state = structuredClone(parsed.state);
    const submitted = { A: Boolean(state.choices?.A), B: Boolean(state.choices?.B) };
    delete state.choices;
    state.players.A.wallet = this.wallets.balance(state.players.A.userId);
    state.players.B.wallet = this.wallets.balance(state.players.B.userId);
    const anchor = this.db.prepare("SELECT cloud_anchor_id AS cloudAnchorId FROM battle_anchors WHERE battle_id = ?")
      .get(state.id);
    return {
      ...state,
      youAre: side,
      serverNow,
      deadlineAt: parsed.row.deadline_at,
      cloudAnchorId: anchor?.cloudAnchorId ?? null,
      choiceSubmitted: submitted[side],
      opponentChoiceSubmitted: submitted[side === "A" ? "B" : "A"]
    };
  }

  publishCloudAnchor(battleId, userId, cloudAnchorId, nowMs = this.now()) {
    const side = this.assertParticipant(battleId, userId);
    if (side !== "A") throw new Error("only nearby battle host can publish cloud anchor");
    const battle = parseBattle(this.db.prepare("SELECT * FROM battles WHERE id = ?").get(battleId));
    if (!battle || battle.state.mode !== "NEARBY") throw new Error("cloud anchor is only available for nearby battle");
    const value = String(cloudAnchorId ?? "").trim();
    if (!/^[A-Za-z0-9_-]{8,256}$/.test(value)) throw new Error("invalid cloud anchor id");
    this.db.prepare(`
      INSERT INTO battle_anchors(battle_id, cloud_anchor_id, host_user_id, updated_at)
      VALUES (?, ?, ?, ?)
      ON CONFLICT(battle_id) DO UPDATE SET
        cloud_anchor_id = excluded.cloud_anchor_id,
        host_user_id = excluded.host_user_id,
        updated_at = excluded.updated_at
    `).run(battleId, value, userId, nowMs);
    this.emit(`battle:${battleId}`, { type: "CLOUD_ANCHOR_READY" });
    return { cloudAnchorId: value };
  }

  assertParticipant(battleId, userId) {
    const row = this.db.prepare(`
      SELECT side FROM battle_users WHERE battle_id = ? AND user_id = ?
    `).get(battleId, userId);
    if (!row) throw new Error("battle not found or access denied");
    return row.side;
  }

  schedule(battleId) {
    this.clearTimer(battleId);
    const row = this.db.prepare("SELECT status, deadline_at FROM battles WHERE id = ?").get(battleId);
    if (!row || row.status !== "ACTIVE") return;
    const delay = Math.max(0, row.deadline_at - this.now());
    const timer = setTimeout(() => {
      this.timers.delete(battleId);
      try {
        this.resolveExpired(battleId, this.now());
      } catch (error) {
        this.emit("error", error);
      }
    }, delay);
    timer.unref?.();
    this.timers.set(battleId, timer);
  }

  clearTimer(battleId) {
    const timer = this.timers.get(battleId);
    if (timer) clearTimeout(timer);
    this.timers.delete(battleId);
  }

  recover() {
    const rows = this.db.prepare("SELECT id FROM battles WHERE status = 'ACTIVE'").all();
    for (const row of rows) {
      if (!this.resolveExpired(row.id, this.now())) this.schedule(row.id);
    }
  }

  close() {
    for (const timer of this.timers.values()) clearTimeout(timer);
    this.timers.clear();
    this.removeAllListeners();
  }
}
