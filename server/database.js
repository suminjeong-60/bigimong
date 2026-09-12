import { DatabaseSync } from "node:sqlite";

export function openDatabase(path = ":memory:") {
  const db = new DatabaseSync(path);
  db.exec("PRAGMA foreign_keys = ON");
  db.exec("PRAGMA journal_mode = WAL");
  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id TEXT PRIMARY KEY,
      token_hash TEXT NOT NULL UNIQUE,
      public_dragon_name TEXT NOT NULL,
      owner_pet_name TEXT NOT NULL,
      created_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS dragons (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
      species TEXT NOT NULL,
      level INTEGER NOT NULL,
      experience INTEGER NOT NULL,
      attack INTEGER NOT NULL,
      evasion INTEGER NOT NULL,
      vitality INTEGER NOT NULL,
      created_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS wallets (
      user_id TEXT PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
      balance INTEGER NOT NULL DEFAULT 0 CHECK(balance >= 0)
    );

    CREATE TABLE IF NOT EXISTS wallet_transactions (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      amount INTEGER NOT NULL,
      type TEXT NOT NULL,
      reference_id TEXT,
      idempotency_key TEXT NOT NULL,
      created_at INTEGER NOT NULL,
      UNIQUE(user_id, idempotency_key)
    );

    CREATE TABLE IF NOT EXISTS daily_counters (
      user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      day_kst TEXT NOT NULL,
      rewarded_steps INTEGER NOT NULL DEFAULT 0,
      remote_paid_battles INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY(user_id, day_kst)
    );

    CREATE TABLE IF NOT EXISTS step_sync_state (
      user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      day_kst TEXT NOT NULL,
      last_total_steps INTEGER NOT NULL DEFAULT 0,
      updated_at INTEGER NOT NULL,
      PRIMARY KEY(user_id, day_kst)
    );

    CREATE TABLE IF NOT EXISTS step_sync_requests (
      user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      idempotency_key TEXT NOT NULL,
      day_kst TEXT NOT NULL,
      reported_total_steps INTEGER NOT NULL,
      rewarded_steps INTEGER NOT NULL,
      egg_progress_added INTEGER NOT NULL DEFAULT 0,
      created_at INTEGER NOT NULL,
      PRIMARY KEY(user_id, idempotency_key)
    );

    CREATE TABLE IF NOT EXISTS journeys (
      user_id TEXT PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
      phase TEXT NOT NULL,
      gift_opened INTEGER NOT NULL DEFAULT 0,
      egg_id TEXT,
      egg_progress INTEGER NOT NULL DEFAULT 0,
      last_cleaned_at INTEGER,
      hatched_at INTEGER,
      eye_color TEXT,
      wing_color TEXT,
      updated_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS battle_queue (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
      mode TEXT NOT NULL,
      stake INTEGER NOT NULL,
      pairing_code TEXT,
      created_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS battles (
      id TEXT PRIMARY KEY,
      mode TEXT NOT NULL,
      stake INTEGER NOT NULL,
      status TEXT NOT NULL,
      state_json TEXT NOT NULL,
      deadline_at INTEGER NOT NULL,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS battle_users (
      battle_id TEXT NOT NULL REFERENCES battles(id) ON DELETE CASCADE,
      user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      side TEXT NOT NULL,
      PRIMARY KEY(battle_id, user_id),
      UNIQUE(battle_id, side)
    );

    CREATE TABLE IF NOT EXISTS battle_anchors (
      battle_id TEXT PRIMARY KEY REFERENCES battles(id) ON DELETE CASCADE,
      cloud_anchor_id TEXT NOT NULL,
      host_user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      updated_at INTEGER NOT NULL
    );

    CREATE INDEX IF NOT EXISTS idx_wallet_transactions_user
      ON wallet_transactions(user_id, created_at DESC);
    CREATE INDEX IF NOT EXISTS idx_battle_users_user
      ON battle_users(user_id, battle_id);
  `);

  const requestColumns = db.prepare("PRAGMA table_info(step_sync_requests)").all();
  if (!requestColumns.some((column) => column.name === "egg_progress_added")) {
    db.exec("ALTER TABLE step_sync_requests ADD COLUMN egg_progress_added INTEGER NOT NULL DEFAULT 0");
  }
  const queueColumns = db.prepare("PRAGMA table_info(battle_queue)").all();
  if (!queueColumns.some((column) => column.name === "pairing_code")) {
    db.exec("ALTER TABLE battle_queue ADD COLUMN pairing_code TEXT");
  }
  db.exec(`
    DROP INDEX IF EXISTS idx_battle_queue_match;
    CREATE INDEX IF NOT EXISTS idx_battle_queue_match
      ON battle_queue(mode, stake, pairing_code, created_at)
  `);
  db.exec(`
    INSERT INTO journeys(
      user_id, phase, gift_opened, egg_id, egg_progress, last_cleaned_at,
      hatched_at, eye_color, wing_color, updated_at
    )
    SELECT id, 'HATCHED', 1, NULL, 30000, NULL, created_at, 'amber', 'forest', created_at
    FROM users
    WHERE NOT EXISTS (SELECT 1 FROM journeys WHERE journeys.user_id = users.id)
  `);
  return db;
}

export function inTransaction(db, work) {
  db.exec("BEGIN IMMEDIATE");
  try {
    const result = work();
    db.exec("COMMIT");
    return result;
  } catch (error) {
    db.exec("ROLLBACK");
    throw error;
  }
}

export function kstDay(nowMs = Date.now()) {
  return new Date(nowMs + 9 * 60 * 60 * 1000).toISOString().slice(0, 10);
}
