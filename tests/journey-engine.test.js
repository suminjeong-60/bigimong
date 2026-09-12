import test from "node:test";
import assert from "node:assert/strict";
import {
  EYE_COLORS,
  SPECIES_CATALOG,
  WING_COLORS,
  applyJourneySteps,
  cleanJourneyEgg,
  createJourneyState,
  enterDragonHome,
  feedJourneyDragon,
  hatchJourneyEgg,
  openStarterGift,
  setJourneyNames
} from "../src/journey-engine.js";

function namedState() {
  return setJourneyNames(createJourneyState(), { dragonName: "라온", ownerPetName: "대장" });
}

test("catalog contains thirty unique species", () => {
  assert.equal(SPECIES_CATALOG.length, 30);
  assert.equal(new Set(SPECIES_CATALOG.map((item) => item.key)).size, 30);
  assert.deepEqual(SPECIES_CATALOG.map((item) => item.artId), Array.from({ length: 30 }, (_, index) => index + 1));
});

test("dragon name and private owner pet name are stored separately", () => {
  const state = namedState();
  assert.equal(state.profile.dragonName, "라온");
  assert.equal(state.profile.ownerPetName, "대장");
  assert.equal(state.phase, "GIFT");
});

test("starter gift can be opened only once", () => {
  const state = openStarterGift(namedState());
  assert.equal(state.phase, "EGG");
  assert.equal(state.starterGiftOpened, true);
  assert.throws(() => openStarterGift(state), /unavailable/);
});

test("walking and egg cleaning can complete thirty thousand progress", () => {
  let state = openStarterGift(namedState());
  state = applyJourneySteps(state, 29_500).state;
  state = cleanJourneyEgg(state, 10_000_000);
  assert.equal(state.phase, "HATCH_READY");
  assert.equal(state.egg.progress, 30_000);
});

test("hatching selects species, eye, and wing colors from independent pools", () => {
  let state = openStarterGift(namedState());
  state = applyJourneySteps(state, 30_000).state;
  state = hatchJourneyEgg(state, () => 0);
  assert.equal(state.phase, "REVEAL");
  assert.equal(state.dragon.species, SPECIES_CATALOG[0].key);
  assert.equal(state.dragon.eyeColor.key, EYE_COLORS[0].key);
  assert.equal(state.dragon.wingColor.key, WING_COLORS[0].key);
  assert.equal(state.dragon.name, "라온");
});

test("post-hatch walking earns bigi and food can level the dragon", () => {
  let state = openStarterGift(namedState());
  state = applyJourneySteps(state, 30_000).state;
  state = hatchJourneyEgg(state, () => 0);
  state = enterDragonHome(state);
  state = applyJourneySteps(state, 1_000).state;
  state.dragon.experience = 500;
  const fed = feedJourneyDragon(state, 20_000_000, () => 0);
  assert.equal(fed.state.wallet, 500);
  assert.equal(fed.state.dragon.level, 2);
  assert.equal(fed.state.dragon.stats.attack, 1);
});
