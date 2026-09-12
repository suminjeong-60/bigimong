import { randomUUID } from "node:crypto";
import { RULES } from "../src/game-engine.js";
import { EYE_COLORS, SPECIES_CATALOG, WING_COLORS } from "../src/journey-engine.js";
import { inTransaction } from "./database.js";

function pick(items, rng) {
  return items[Math.min(items.length - 1, Math.floor(rng() * items.length))];
}

export class JourneyService {
  constructor(db, accountService, { rng = Math.random, now = Date.now } = {}) {
    this.db = db;
    this.accounts = accountService;
    this.rng = rng;
    this.now = now;
  }

  openGift(userId, nowMs = this.now()) {
    inTransaction(this.db, () => {
      const journey = this.row(userId);
      if (journey.phase !== "GIFT" || journey.gift_opened) throw new Error("starter gift unavailable");
      this.db.prepare(`
        UPDATE journeys
        SET phase = 'EGG', gift_opened = 1, egg_id = ?, egg_progress = 0, updated_at = ?
        WHERE user_id = ?
      `).run(randomUUID(), nowMs, userId);
    });
    return this.accounts.getById(userId);
  }

  cleanEgg(userId, nowMs = this.now()) {
    inTransaction(this.db, () => {
      const journey = this.row(userId);
      if (journey.phase !== "EGG") throw new Error("egg cannot be cleaned now");
      if (journey.last_cleaned_at !== null && nowMs - journey.last_cleaned_at < RULES.eggCleanCooldownMs) {
        throw new Error("egg clean cooldown active");
      }
      const progress = Math.min(RULES.eggProgressRequired, journey.egg_progress + RULES.eggCleanBonus);
      const phase = progress === RULES.eggProgressRequired ? "READY_TO_HATCH" : "EGG";
      this.db.prepare(`
        UPDATE journeys
        SET phase = ?, egg_progress = ?, last_cleaned_at = ?, updated_at = ?
        WHERE user_id = ?
      `).run(phase, progress, nowMs, nowMs, userId);
    });
    return this.accounts.getById(userId);
  }

  hatch(userId, nowMs = this.now()) {
    inTransaction(this.db, () => {
      const journey = this.row(userId);
      if (journey.phase !== "READY_TO_HATCH" || journey.egg_progress < RULES.eggProgressRequired) {
        throw new Error("egg is not ready to hatch");
      }
      const species = pick(SPECIES_CATALOG, this.rng);
      const eyeColor = pick(EYE_COLORS, this.rng);
      const wingColor = pick(WING_COLORS, this.rng);
      this.db.prepare("UPDATE dragons SET species = ? WHERE user_id = ?").run(species.key, userId);
      this.db.prepare(`
        UPDATE journeys
        SET phase = 'HATCHED', hatched_at = ?, eye_color = ?, wing_color = ?, updated_at = ?
        WHERE user_id = ?
      `).run(nowMs, eyeColor.key, wingColor.key, nowMs, userId);
    });
    return this.accounts.getById(userId);
  }

  row(userId) {
    const row = this.db.prepare("SELECT * FROM journeys WHERE user_id = ?").get(userId);
    if (!row) throw new Error("journey not found");
    return row;
  }
}
