import test from "node:test";
import assert from "node:assert/strict";
import {
  RULES,
  addEggSteps,
  cleanEgg,
  createBattle,
  createDragon,
  createEgg,
  earnBigiFromSteps,
  feedDragon,
  healthForVitality,
  resolveRound,
  stageForLevel,
  validateBattleEntry,
  wakeDragonWithSteps
} from "../src/game-engine.js";

const dragon = (overrides = {}) => createDragon({
  id: overrides.id ?? "d1",
  name: overrides.name ?? "라온",
  ownerPetName: "대장",
  species: overrides.species ?? "tyrannosaurus",
  level: overrides.level ?? 1,
  experience: overrides.experience ?? 0,
  attack: overrides.attack ?? 0,
  evasion: overrides.evasion ?? 0,
  vitality: overrides.vitality ?? 0,
  lastFedAt: overrides.lastFedAt ?? null
});

const player = (id, pet, wallet = 10_000) => ({ id, wallet, dragon: pet });

test("growth stages use levels 1-9, 10-19, and 20-30", () => {
  assert.equal(stageForLevel(1), "GROWTH");
  assert.equal(stageForLevel(10), "YOUTH");
  assert.equal(stageForLevel(20), "ADULT");
});

test("egg hatches at thirty thousand progress and cleaning adds five hundred", () => {
  let egg = createEgg({ id: "egg-1", progress: 29_000 });
  egg = cleanEgg(egg, 1_000_000);
  assert.equal(egg.progress, 29_500);
  egg = addEggSteps(egg, 500);
  assert.equal(egg.progress, 30_000);
  assert.equal(egg.hatched, true);
});

test("egg cleaning respects the two hour cooldown", () => {
  const egg = createEgg({ id: "egg-1", lastCleanedAt: 1_000 });
  assert.throws(() => cleanEgg(egg, 2_000), /cooldown/);
});

test("vitality can never provide more than five hp", () => {
  assert.equal(healthForVitality(0), 3);
  assert.equal(healthForVitality(5), 4);
  assert.equal(healthForVitality(10), 5);
  assert.throws(() => healthForVitality(11), /exceeds cap/);
});

test("feeding levels up and awards one deterministic random stat", () => {
  const result = feedDragon({ wallet: 2_000, dragon: dragon({ experience: 500 }) }, 1_000_000, () => 0);
  assert.equal(result.wallet, 1_500);
  assert.equal(result.dragon.level, 2);
  assert.equal(result.dragon.stats.attack, 1);
  assert.deepEqual(result.levelUps, [{ level: 2, stat: "attack" }]);
});

test("feeding respects the one hour cooldown", () => {
  assert.throws(
    () => feedDragon({ wallet: 2_000, dragon: dragon({ lastFedAt: 1000 }) }, 2000),
    /cooldown/
  );
});

test("a sleeping dragon wakes only after one thousand new steps", () => {
  const sleeping = dragon({ lastFedAt: 1_000 });
  const now = 1_000 + RULES.sleepAfterMs;
  assert.equal(wakeDragonWithSteps(sleeping, now, 999).awakened, false);
  assert.equal(wakeDragonWithSteps(sleeping, now, 999).remainingSteps, 1);
  assert.equal(wakeDragonWithSteps(sleeping, now, 1000).awakened, true);
});

test("attack and evasion stat caps are enforced", () => {
  assert.throws(() => dragon({ attack: 21 }), /attack exceeds cap/);
  assert.throws(() => dragon({ evasion: 16 }), /evasion exceeds cap/);
});

test("walking rewards at most ten thousand bigi per day", () => {
  const result = earnBigiFromSteps({ wallet: 0, rewardedStepsToday: 9_500 }, 2_000);
  assert.equal(result.rewarded, 500);
  assert.equal(result.wallet, 500);
  assert.equal(result.rewardedStepsToday, RULES.dailyStepRewardCap);
});

test("remote stake is capped at 100 and paid remote battles at 20", () => {
  assert.equal(validateBattleEntry({ mode: "REMOTE", stake: 100, remoteBattlesToday: 19 }), true);
  assert.throws(() => validateBattleEntry({ mode: "REMOTE", stake: 101 }), /stake/);
  assert.throws(
    () => validateBattleEntry({ mode: "REMOTE", stake: 100, remoteBattlesToday: 20 }),
    /daily battle limit/
  );
  assert.equal(validateBattleEntry({ mode: "REMOTE", stake: 0, remoteBattlesToday: 20 }), true);
});

test("nearby battle accepts a 1000 bigi stake", () => {
  assert.equal(validateBattleEntry({ mode: "NEARBY", stake: 1000 }), true);
});

test("growth and adult dragons can enter the same wager battle", () => {
  const battle = createBattle({
    playerA: player("A", dragon({ id: "child", level: 1 })),
    playerB: player("B", dragon({ id: "adult", level: 30 })),
    mode: "REMOTE",
    stake: 100
  });
  assert.equal(battle.status, "ACTIVE");
  assert.equal(battle.players.A.dragon.stage, "GROWTH");
  assert.equal(battle.players.B.dragon.stage, "ADULT");
});

test("missing input is replaced by server automatic play", () => {
  const battle = createBattle({
    playerA: player("A", dragon({ id: "a" })),
    playerB: player("B", dragon({ id: "b" })),
    mode: "REMOTE",
    stake: 0
  });
  const { event } = resolveRound(battle, {}, () => 0);
  assert.equal(event.attackAutomatic, true);
  assert.equal(event.defendAutomatic, true);
  assert.equal(event.attackDirection, "LEFT");
});

test("maximum vitality takes five normal landed hits and no more", () => {
  let battle = createBattle({
    playerA: player("A", dragon({ id: "a" })),
    playerB: player("B", dragon({ id: "b", vitality: 10 })),
    mode: "REMOTE",
    stake: 0
  });

  let hitsOnB = 0;
  while (battle.status === "ACTIVE") {
    const aAttacks = battle.attacker === "A";
    const choices = aAttacks
      ? { attackDirection: "LEFT", defendDirection: "LEFT" }
      : { attackDirection: "LEFT", defendDirection: "RIGHT" };
    const result = resolveRound(battle, choices, () => 0.99);
    battle = result.battle;
    if (aAttacks && result.event.damage === 1) hitsOnB += 1;
  }
  assert.equal(hitsOnB, 5);
  assert.equal(battle.winner, "A");
});

test("twenty percent of the total pot is burned", () => {
  let battle = createBattle({
    playerA: player("A", dragon({ id: "a" }), 1000),
    playerB: player("B", dragon({ id: "b" }), 1000),
    mode: "REMOTE",
    stake: 100
  });
  while (battle.status === "ACTIVE") {
    const result = resolveRound(
      battle,
      { attackDirection: "CENTER", defendDirection: "CENTER" },
      () => 0.99
    );
    battle = result.battle;
  }
  assert.equal(battle.burned, 40);
  assert.equal(battle.payout, 160);
});
