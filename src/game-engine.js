export const RULES = Object.freeze({
  eggProgressRequired: 30_000,
  eggCleanBonus: 500,
  eggCleanCooldownMs: 2 * 60 * 60 * 1000,
  maxLevel: 30,
  feedCost: 500,
  feedXp: 100,
  feedCooldownMs: 60 * 60 * 1000,
  sleepAfterMs: 24 * 60 * 60 * 1000,
  wakeSteps: 1000,
  dailyStepRewardCap: 10_000,
  burnRate: 0.2,
  turnSeconds: 10,
  statCaps: Object.freeze({ attack: 20, evasion: 15, vitality: 10 }),
  battleModes: Object.freeze({
    NEARBY: Object.freeze({ maxStake: 1000, dailyLimit: null }),
    REMOTE: Object.freeze({ maxStake: 100, dailyLimit: 20 })
  })
});

export const DIRECTIONS = Object.freeze(["LEFT", "CENTER", "RIGHT"]);

export const SPECIES_SKILLS = Object.freeze({
  tyrannosaurus: Object.freeze({ name: "폭군의 송곳니" }),
  triceratops: Object.freeze({ name: "대지의 돌진" }),
  pterosaur: Object.freeze({ name: "천공 급강하" }),
  stegosaurus: Object.freeze({ name: "가시 폭풍" }),
  ankylosaurus: Object.freeze({ name: "철갑의 철퇴" })
});

function assertInteger(value, name, min = 0) {
  if (!Number.isInteger(value) || value < min) {
    throw new Error(`${name} must be an integer >= ${min}`);
  }
}

function clone(value) {
  return structuredClone(value);
}

function randomIndex(length, rng) {
  return Math.min(length - 1, Math.floor(rng() * length));
}

function automaticDirection(rng) {
  return DIRECTIONS[randomIndex(DIRECTIONS.length, rng)];
}

export function stageForLevel(level) {
  assertInteger(level, "level", 1);
  if (level > RULES.maxLevel) throw new Error("level exceeds max level");
  if (level <= 9) return "GROWTH";
  if (level <= 19) return "YOUTH";
  return "ADULT";
}

export function createEgg({ id, progress = 0, lastCleanedAt = null }) {
  if (!id) throw new Error("egg id is required");
  assertInteger(progress, "progress");
  const capped = Math.min(progress, RULES.eggProgressRequired);
  return { id, progress: capped, lastCleanedAt, hatched: capped === RULES.eggProgressRequired };
}

export function addEggSteps(egg, verifiedSteps) {
  assertInteger(verifiedSteps, "verifiedSteps");
  const next = clone(egg);
  next.progress = Math.min(RULES.eggProgressRequired, next.progress + verifiedSteps);
  next.hatched = next.progress === RULES.eggProgressRequired;
  return next;
}

export function cleanEgg(egg, nowMs) {
  if (egg.hatched) throw new Error("egg already hatched");
  if (egg.lastCleanedAt !== null && nowMs - egg.lastCleanedAt < RULES.eggCleanCooldownMs) {
    throw new Error("egg clean cooldown active");
  }
  const next = clone(egg);
  next.lastCleanedAt = nowMs;
  next.progress = Math.min(RULES.eggProgressRequired, next.progress + RULES.eggCleanBonus);
  next.hatched = next.progress === RULES.eggProgressRequired;
  return next;
}

export function xpForNextLevel(level) {
  if (level >= RULES.maxLevel) return null;
  return 500 + level * 100;
}

export function healthForVitality(vitality) {
  assertInteger(vitality, "vitality");
  if (vitality > RULES.statCaps.vitality) throw new Error("vitality exceeds cap");
  if (vitality === RULES.statCaps.vitality) return 5;
  if (vitality >= 5) return 4;
  return 3;
}

export function createDragon({
  id,
  name,
  ownerPetName,
  species,
  level = 1,
  experience = 0,
  attack = 0,
  evasion = 0,
  vitality = 0,
  lastFedAt = null
}) {
  if (!id || !name || !ownerPetName || !species) throw new Error("dragon identity is incomplete");
  assertInteger(level, "level", 1);
  assertInteger(experience, "experience");
  for (const [stat, value] of Object.entries({ attack, evasion, vitality })) {
    assertInteger(value, stat);
    if (value > RULES.statCaps[stat]) throw new Error(`${stat} exceeds cap`);
  }
  return {
    id,
    name,
    ownerPetName,
    species,
    level,
    stage: stageForLevel(level),
    experience,
    stats: { attack, evasion, vitality },
    lastFedAt
  };
}

export function motionKey(dragon, action) {
  if (!["attack", "dodge", "hit", "win", "lose"].includes(action)) {
    throw new Error("unsupported motion action");
  }
  return `${dragon.species}.${dragon.stage.toLowerCase()}.${action}`;
}

export function awardRandomStat(dragon, rng = Math.random) {
  const next = clone(dragon);
  const eligible = Object.entries(next.stats)
    .filter(([stat, value]) => value < RULES.statCaps[stat])
    .map(([stat]) => stat);
  if (eligible.length === 0) return { dragon: next, awardedStat: null };
  const awardedStat = eligible[randomIndex(eligible.length, rng)];
  next.stats[awardedStat] += 1;
  return { dragon: next, awardedStat };
}

export function feedDragon({ wallet, dragon }, nowMs, rng = Math.random) {
  assertInteger(wallet, "wallet");
  if (wallet < RULES.feedCost) throw new Error("insufficient bigi");
  if (dragon.lastFedAt !== null && nowMs - dragon.lastFedAt < RULES.feedCooldownMs) {
    throw new Error("feed cooldown active");
  }

  let nextDragon = clone(dragon);
  nextDragon.lastFedAt = nowMs;
  const levelUps = [];

  if (nextDragon.level < RULES.maxLevel) {
    nextDragon.experience += RULES.feedXp;
    while (nextDragon.level < RULES.maxLevel) {
      const needed = xpForNextLevel(nextDragon.level);
      if (nextDragon.experience < needed) break;
      nextDragon.experience -= needed;
      nextDragon.level += 1;
      nextDragon.stage = stageForLevel(nextDragon.level);
      const result = awardRandomStat(nextDragon, rng);
      nextDragon = result.dragon;
      levelUps.push({ level: nextDragon.level, stat: result.awardedStat });
    }
  }

  return { wallet: wallet - RULES.feedCost, dragon: nextDragon, levelUps };
}

export function isSleeping(dragon, nowMs) {
  return dragon.lastFedAt !== null && nowMs - dragon.lastFedAt >= RULES.sleepAfterMs;
}

export function wakeDragonWithSteps(dragon, nowMs, verifiedStepsSinceSleep) {
  assertInteger(verifiedStepsSinceSleep, "verifiedStepsSinceSleep");
  if (!isSleeping(dragon, nowMs)) return { dragon: clone(dragon), awakened: false, remainingSteps: 0 };
  const remainingSteps = Math.max(0, RULES.wakeSteps - verifiedStepsSinceSleep);
  if (remainingSteps > 0) return { dragon: clone(dragon), awakened: false, remainingSteps };
  const next = clone(dragon);
  next.lastFedAt = nowMs;
  return { dragon: next, awakened: true, remainingSteps: 0 };
}

export function earnBigiFromSteps({ wallet, rewardedStepsToday }, verifiedSteps) {
  assertInteger(wallet, "wallet");
  assertInteger(rewardedStepsToday, "rewardedStepsToday");
  assertInteger(verifiedSteps, "verifiedSteps");
  const remaining = Math.max(0, RULES.dailyStepRewardCap - rewardedStepsToday);
  const rewarded = Math.min(remaining, verifiedSteps);
  return {
    wallet: wallet + rewarded,
    rewardedStepsToday: rewardedStepsToday + rewarded,
    rewarded
  };
}

export function validateBattleEntry({ mode, stake, remoteBattlesToday = 0 }) {
  const modeRules = RULES.battleModes[mode];
  if (!modeRules) throw new Error("unknown battle mode");
  assertInteger(stake, "stake");
  assertInteger(remoteBattlesToday, "remoteBattlesToday");
  if (stake > modeRules.maxStake) throw new Error("stake exceeds mode limit");
  if (mode === "REMOTE" && stake > 0 && remoteBattlesToday >= modeRules.dailyLimit) {
    throw new Error("remote daily battle limit reached");
  }
  return true;
}

export function createBattle({
  playerA,
  playerB,
  mode,
  stake,
  remoteBattlesTodayA = 0,
  remoteBattlesTodayB = 0
}) {
  validateBattleEntry({ mode, stake, remoteBattlesToday: remoteBattlesTodayA });
  validateBattleEntry({ mode, stake, remoteBattlesToday: remoteBattlesTodayB });
  if (playerA.wallet < stake || playerB.wallet < stake) throw new Error("insufficient battle stake");

  const a = clone(playerA);
  const b = clone(playerB);
  a.wallet -= stake;
  b.wallet -= stake;

  return {
    id: `battle-${Date.now()}`,
    mode,
    stake,
    status: "ACTIVE",
    round: 1,
    attacker: "A",
    players: {
      A: { ...a, hp: healthForVitality(a.dragon.stats.vitality) },
      B: { ...b, hp: healthForVitality(b.dragon.stats.vitality) }
    },
    history: [],
    winner: null,
    burned: 0,
    payout: 0
  };
}

function normalizeDirection(direction, rng) {
  if (DIRECTIONS.includes(direction)) return { direction, automatic: false };
  return { direction: automaticDirection(rng), automatic: true };
}

export function resolveRound(battle, choices = {}, rng = Math.random) {
  if (battle.status !== "ACTIVE") throw new Error("battle is not active");
  const next = clone(battle);
  const attackerSide = next.attacker;
  const defenderSide = attackerSide === "A" ? "B" : "A";
  const attacker = next.players[attackerSide];
  const defender = next.players[defenderSide];
  const attackChoice = normalizeDirection(choices.attackDirection, rng);
  const defendChoice = normalizeDirection(choices.defendDirection, rng);

  let outcome = "MANUAL_DODGE";
  let damage = 0;
  let critical = false;
  let statDodge = false;

  if (attackChoice.direction === defendChoice.direction) {
    const evasionChance = defender.dragon.stats.evasion / 100;
    statDodge = rng() < evasionChance;
    if (statDodge) {
      outcome = "STAT_DODGE";
    } else {
      const criticalChance = attacker.dragon.stats.attack / 100;
      critical = rng() < criticalChance;
      damage = critical ? 2 : 1;
      defender.hp = Math.max(0, defender.hp - damage);
      outcome = critical ? "CRITICAL_HIT" : "HIT";
    }
  }

  const event = {
    round: next.round,
    attacker: attackerSide,
    defender: defenderSide,
    attackDirection: attackChoice.direction,
    defendDirection: defendChoice.direction,
    attackAutomatic: attackChoice.automatic,
    defendAutomatic: defendChoice.automatic,
    outcome,
    damage,
    critical,
    statDodge,
    motion: {
      attacker: motionKey(attacker.dragon, "attack"),
      defender: motionKey(defender.dragon, outcome.includes("DODGE") ? "dodge" : "hit")
    }
  };
  next.history.push(event);

  if (defender.hp === 0) {
    next.status = "FINISHED";
    next.winner = attackerSide;
    const pot = next.stake * 2;
    next.burned = Math.floor(pot * RULES.burnRate);
    next.payout = pot - next.burned;
    next.players[attackerSide].wallet += next.payout;
  } else {
    next.attacker = defenderSide;
    next.round += 1;
  }

  return { battle: next, event };
}
