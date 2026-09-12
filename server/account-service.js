import { createHash, randomBytes, randomUUID } from "node:crypto";
import { createDragon } from "../src/game-engine.js";
import { EYE_COLORS, LEGACY_SPECIES_ALIASES, SPECIES_CATALOG, WING_COLORS } from "../src/journey-engine.js";
import { inTransaction } from "./database.js";

function tokenHash(token) {
  return createHash("sha256").update(token).digest("hex");
}

function cleanName(value, field) {
  const result = String(value ?? "").trim();
  if (result.length < 1 || result.length > 12) throw new Error(`${field} must be 1-12 characters`);
  return result;
}

function catalogSpecies(key) {
  const canonicalKey = LEGACY_SPECIES_ALIASES[key] ?? key;
  const species = SPECIES_CATALOG.find((item) => item.key === canonicalKey);
  if (!species) throw new Error("unknown species");
  return species;
}

export class AccountService {
  constructor(db) {
    this.db = db;
  }

  createGuest({ dragonName, ownerPetName, species = "tyrannosaurus", developmentStats = null }, nowMs = Date.now()) {
    const publicDragonName = cleanName(dragonName, "dragonName");
    const privateOwnerPetName = cleanName(ownerPetName, "ownerPetName");
    catalogSpecies(species);
    const userId = randomUUID();
    const dragonId = randomUUID();
    const token = randomBytes(32).toString("base64url");
    const stats = developmentStats ?? {};
    const dragon = createDragon({
      id: dragonId,
      name: publicDragonName,
      ownerPetName: privateOwnerPetName,
      species,
      level: stats.level ?? 1,
      experience: stats.experience ?? 0,
      attack: stats.attack ?? 0,
      evasion: stats.evasion ?? 0,
      vitality: stats.vitality ?? 0
    });

    inTransaction(this.db, () => {
      this.db.prepare(`
        INSERT INTO users(id, token_hash, public_dragon_name, owner_pet_name, created_at)
        VALUES (?, ?, ?, ?, ?)
      `).run(userId, tokenHash(token), publicDragonName, privateOwnerPetName, nowMs);
      this.db.prepare(`
        INSERT INTO dragons(id, user_id, species, level, experience, attack, evasion, vitality, created_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
      `).run(
        dragonId,
        userId,
        dragon.species,
        dragon.level,
        dragon.experience,
        dragon.stats.attack,
        dragon.stats.evasion,
        dragon.stats.vitality,
        nowMs
      );
      this.db.prepare("INSERT INTO wallets(user_id, balance) VALUES (?, 0)").run(userId);
      this.db.prepare(`
        INSERT INTO journeys(
          user_id, phase, gift_opened, egg_id, egg_progress, last_cleaned_at,
          hatched_at, eye_color, wing_color, updated_at
        ) VALUES (?, ?, ?, NULL, ?, NULL, ?, ?, ?, ?)
      `).run(
        userId,
        developmentStats ? "HATCHED" : "GIFT",
        developmentStats ? 1 : 0,
        developmentStats ? 30_000 : 0,
        developmentStats ? nowMs : null,
        developmentStats ? EYE_COLORS[0].key : null,
        developmentStats ? WING_COLORS[0].key : null,
        nowMs
      );
    });

    return { token, user: this.getById(userId) };
  }

  authenticate(token) {
    if (!token) return null;
    const row = this.db.prepare("SELECT id FROM users WHERE token_hash = ?").get(tokenHash(token));
    return row ? this.getById(row.id) : null;
  }

  getById(userId) {
    const row = this.db.prepare(`
      SELECT
        u.id AS user_id,
        u.public_dragon_name,
        u.owner_pet_name,
        d.id AS dragon_id,
        d.species,
        d.level,
        d.experience,
        d.attack,
        d.evasion,
        d.vitality,
        w.balance,
        j.phase,
        j.gift_opened,
        j.egg_id,
        j.egg_progress,
        j.last_cleaned_at,
        j.hatched_at,
        j.eye_color,
        j.wing_color
      FROM users u
      JOIN dragons d ON d.user_id = u.id
      JOIN wallets w ON w.user_id = u.id
      JOIN journeys j ON j.user_id = u.id
      WHERE u.id = ?
    `).get(userId);
    if (!row) return null;
    const species = catalogSpecies(row.species);
    const eyeColor = EYE_COLORS.find((item) => item.key === row.eye_color) ?? null;
    const wingColor = WING_COLORS.find((item) => item.key === row.wing_color) ?? null;
    const journey = {
      phase: row.phase,
      giftOpened: Boolean(row.gift_opened),
      egg: row.egg_id ? {
        id: row.egg_id,
        progress: row.egg_progress,
        required: 30_000,
        lastCleanedAt: row.last_cleaned_at,
        nextCleanAt: row.last_cleaned_at === null ? null : row.last_cleaned_at + 2 * 60 * 60 * 1000
      } : null
    };
    return {
      id: row.user_id,
      wallet: row.balance,
      journey,
      dragon: row.phase === "HATCHED" ? {
        ...createDragon({
        id: row.dragon_id,
        name: row.public_dragon_name,
        ownerPetName: row.owner_pet_name,
        species: row.species,
        level: row.level,
        experience: row.experience,
        attack: row.attack,
        evasion: row.evasion,
        vitality: row.vitality
        }),
        speciesName: species.name,
        skillName: species.skill,
        emoji: species.emoji,
        artId: species.artId,
        eyeColor,
        wingColor
      } : null
    };
  }
}
