import { randomUUID } from "node:crypto";
import { RULES } from "../src/game-engine.js";
import { inTransaction, kstDay } from "./database.js";

export class WalletService {
  constructor(db) {
    this.db = db;
  }

  balance(userId) {
    const row = this.db.prepare("SELECT balance FROM wallets WHERE user_id = ?").get(userId);
    if (!row) throw new Error("wallet not found");
    return row.balance;
  }

  apply(userId, { amount, type, referenceId = null, idempotencyKey, nowMs = Date.now() }) {
    if (!Number.isInteger(amount) || amount === 0) throw new Error("amount must be a non-zero integer");
    if (!idempotencyKey) throw new Error("idempotency key is required");

    return inTransaction(this.db, () => this.applyInsideTransaction(userId, {
      amount,
      type,
      referenceId,
      idempotencyKey,
      nowMs
    }));
  }

  applyInsideTransaction(userId, { amount, type, referenceId = null, idempotencyKey, nowMs }) {
    const existing = this.db.prepare(`
      SELECT id, amount FROM wallet_transactions WHERE user_id = ? AND idempotency_key = ?
    `).get(userId, idempotencyKey);
    if (existing) return { applied: false, balance: this.balance(userId), transactionId: existing.id };

    const current = this.balance(userId);
    const next = current + amount;
    if (next < 0) throw new Error("insufficient bigi");
    const transactionId = randomUUID();
    this.db.prepare("UPDATE wallets SET balance = ? WHERE user_id = ?").run(next, userId);
    this.db.prepare(`
      INSERT INTO wallet_transactions(id, user_id, amount, type, reference_id, idempotency_key, created_at)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `).run(transactionId, userId, amount, type, referenceId, idempotencyKey, nowMs);
    return { applied: true, balance: next, transactionId };
  }

  rewardDevelopmentSteps(userId, verifiedSteps, idempotencyKey, nowMs = Date.now()) {
    if (!Number.isInteger(verifiedSteps) || verifiedSteps < 0) throw new Error("invalid verified steps");
    const day = kstDay(nowMs);
    return inTransaction(this.db, () => {
      const duplicate = this.db.prepare(`
        SELECT id FROM wallet_transactions WHERE user_id = ? AND idempotency_key = ?
      `).get(userId, idempotencyKey);
      if (duplicate) {
        const counter = this.dailyCounter(userId, nowMs);
        return { applied: false, rewarded: 0, balance: this.balance(userId), counter };
      }

      this.db.prepare(`
        INSERT INTO daily_counters(user_id, day_kst, rewarded_steps, remote_paid_battles)
        VALUES (?, ?, 0, 0)
        ON CONFLICT(user_id, day_kst) DO NOTHING
      `).run(userId, day);
      const counter = this.dailyCounter(userId, nowMs);
      const rewarded = Math.min(verifiedSteps, Math.max(0, RULES.dailyStepRewardCap - counter.rewardedSteps));
      if (rewarded === 0) return { applied: false, rewarded: 0, balance: this.balance(userId), counter };
      this.db.prepare(`
        UPDATE daily_counters SET rewarded_steps = rewarded_steps + ?
        WHERE user_id = ? AND day_kst = ?
      `).run(rewarded, userId, day);
      const transaction = this.applyInsideTransaction(userId, {
        amount: rewarded,
        type: "STEP_REWARD_DEV",
        referenceId: day,
        idempotencyKey,
        nowMs
      });
      return {
        applied: transaction.applied,
        rewarded,
        balance: transaction.balance,
        counter: this.dailyCounter(userId, nowMs)
      };
    });
  }

  syncDailyCumulativeSteps(userId, { day, totalSteps, idempotencyKey }, nowMs = Date.now()) {
    if (day !== kstDay(nowMs)) throw new Error("step day must be today's KST date");
    if (!Number.isInteger(totalSteps) || totalSteps < 0 || totalSteps > 1_000_000) {
      throw new Error("invalid cumulative step total");
    }
    if (typeof idempotencyKey !== "string" || idempotencyKey.length < 1 || idempotencyKey.length > 120) {
      throw new Error("invalid idempotency key");
    }

    return inTransaction(this.db, () => {
      const duplicate = this.db.prepare(`
        SELECT rewarded_steps AS rewarded
        FROM step_sync_requests
        WHERE user_id = ? AND idempotency_key = ?
      `).get(userId, idempotencyKey);
      if (duplicate) {
        return {
          applied: false,
          duplicate: true,
          rewarded: 0,
          balance: this.balance(userId),
          counter: this.dailyCounter(userId, nowMs),
          journey: this.journeyState(userId)
        };
      }

      this.db.prepare(`
        INSERT INTO daily_counters(user_id, day_kst, rewarded_steps, remote_paid_battles)
        VALUES (?, ?, 0, 0)
        ON CONFLICT(user_id, day_kst) DO NOTHING
      `).run(userId, day);
      this.db.prepare(`
        INSERT INTO step_sync_state(user_id, day_kst, last_total_steps, updated_at)
        VALUES (?, ?, 0, ?)
        ON CONFLICT(user_id, day_kst) DO NOTHING
      `).run(userId, day, nowMs);

      const syncState = this.db.prepare(`
        SELECT last_total_steps AS lastTotalSteps
        FROM step_sync_state WHERE user_id = ? AND day_kst = ?
      `).get(userId, day);
      const counter = this.dailyCounter(userId, nowMs);
      const delta = Math.max(0, totalSteps - syncState.lastTotalSteps);
      const journey = this.db.prepare(`
        SELECT phase, egg_progress AS eggProgress FROM journeys WHERE user_id = ?
      `).get(userId);
      if (!journey) throw new Error("journey not found");

      let rewarded = 0;
      let eggProgressAdded = 0;
      let balance = this.balance(userId);
      const consumesReading = journey.phase !== "GIFT";
      if (consumesReading) {
        this.db.prepare(`
          UPDATE step_sync_state
          SET last_total_steps = MAX(last_total_steps, ?), updated_at = ?
          WHERE user_id = ? AND day_kst = ?
        `).run(totalSteps, nowMs, userId, day);
      }

      if (journey.phase === "EGG") {
        eggProgressAdded = Math.min(delta, Math.max(0, RULES.eggProgressRequired - journey.eggProgress));
        const eggProgress = journey.eggProgress + eggProgressAdded;
        this.db.prepare(`
          UPDATE journeys
          SET egg_progress = ?, phase = ?, updated_at = ?
          WHERE user_id = ?
        `).run(
          eggProgress,
          eggProgress === RULES.eggProgressRequired ? "READY_TO_HATCH" : "EGG",
          nowMs,
          userId
        );
      } else if (journey.phase === "HATCHED") {
        rewarded = Math.min(delta, Math.max(0, RULES.dailyStepRewardCap - counter.rewardedSteps));
      }

      if (journey.phase === "HATCHED" && rewarded > 0) {
        this.db.prepare(`
          UPDATE daily_counters SET rewarded_steps = rewarded_steps + ?
          WHERE user_id = ? AND day_kst = ?
        `).run(rewarded, userId, day);
        balance = this.applyInsideTransaction(userId, {
          amount: rewarded,
          type: "HEALTH_CONNECT_STEP_REWARD",
          referenceId: day,
          idempotencyKey: `health:${idempotencyKey}`,
          nowMs
        }).balance;
      }

      this.db.prepare(`
        INSERT INTO step_sync_requests(
          user_id, idempotency_key, day_kst, reported_total_steps,
          rewarded_steps, egg_progress_added, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?)
      `).run(userId, idempotencyKey, day, totalSteps, rewarded, eggProgressAdded, nowMs);

      return {
        applied: rewarded > 0 || eggProgressAdded > 0,
        duplicate: false,
        rewarded,
        eggProgressAdded,
        balance,
        reportedTotalSteps: totalSteps,
        acceptedTotalSteps: consumesReading ? Math.max(syncState.lastTotalSteps, totalSteps) : syncState.lastTotalSteps,
        counter: this.dailyCounter(userId, nowMs),
        journey: this.journeyState(userId)
      };
    });
  }

  journeyState(userId) {
    const row = this.db.prepare(`
      SELECT phase, gift_opened, egg_id, egg_progress, last_cleaned_at
      FROM journeys WHERE user_id = ?
    `).get(userId);
    if (!row) throw new Error("journey not found");
    return {
      phase: row.phase,
      giftOpened: Boolean(row.gift_opened),
      egg: row.egg_id ? {
        id: row.egg_id,
        progress: row.egg_progress,
        required: RULES.eggProgressRequired,
        lastCleanedAt: row.last_cleaned_at,
        nextCleanAt: row.last_cleaned_at === null ? null : row.last_cleaned_at + RULES.eggCleanCooldownMs
      } : null
    };
  }

  dailyCounter(userId, nowMs = Date.now()) {
    const day = kstDay(nowMs);
    const row = this.db.prepare(`
      SELECT rewarded_steps, remote_paid_battles
      FROM daily_counters WHERE user_id = ? AND day_kst = ?
    `).get(userId, day);
    return {
      day,
      rewardedSteps: row?.rewarded_steps ?? 0,
      remotePaidBattles: row?.remote_paid_battles ?? 0
    };
  }

  list(userId, limit = 50) {
    return this.db.prepare(`
      SELECT id, amount, type, reference_id AS referenceId,
             idempotency_key AS idempotencyKey, created_at AS createdAt
      FROM wallet_transactions
      WHERE user_id = ?
      ORDER BY created_at DESC
      LIMIT ?
    `).all(userId, Math.min(Math.max(limit, 1), 100));
  }
}
