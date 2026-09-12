import {
  addEggSteps,
  cleanEgg,
  createDragon,
  createEgg,
  earnBigiFromSteps,
  feedDragon
} from "./game-engine.js";

export const SPECIES_CATALOG = Object.freeze([
  [1, "tyrannosaurus", "티라노사우루스", "폭군의 화염", "🐲"],
  [2, "triceratops", "트리케라톱스", "대지의 돌진", "🦖"],
  [3, "pterosaur", "익룡", "천공 급강하", "🐉"],
  [4, "stegosaurus", "스테고사우루스", "가시 폭풍", "🦕"],
  [5, "brachiosaurus", "브라키오사우루스", "거인의 진동", "🦕"],
  [6, "spinosaurus", "스피노사우루스", "심연의 칼날", "🐲"],
  [7, "ankylosaurus", "안킬로사우루스", "철갑의 철퇴", "🦕"],
  [8, "velociraptor", "벨로키랍토르", "숲불의 발톱", "🦖"],
  [9, "corythosaurus", "코리토사우루스", "공명의 볏", "🦕"],
  [10, "carnotaurus", "카르노타우루스", "붉은 뿔 돌진", "🦖"],
  [11, "mosasaurus", "모사사우루스", "해일의 턱", "🐉"],
  [12, "allosaurus", "알로사우루스", "포식자의 연격", "🦖"],
  [13, "quetzalcoatlus", "케찰코아틀루스", "창공의 창", "🐉"],
  [14, "deinonychus", "데이노니쿠스", "낫 발톱", "🦖"],
  [15, "euoplocephalus", "유오플로케팔루스", "대지의 망치", "🦕"],
  [16, "baryonyx", "바리오닉스", "강의 사냥꾼", "🦖"],
  [17, "oviraptor", "오비랍토르", "알 수호", "🦖"],
  [18, "protoceratops", "프로토케라톱스", "모래 방패", "🦖"],
  [19, "gallimimus", "갈리미무스", "질풍각", "🦖"],
  [20, "dracorex", "드라코렉스", "폭풍 왕관", "🐲"],
  [21, "giganotosaurus", "기가노토사우루스", "파멸의 화염턱", "🦖"],
  [22, "dilophosaurus", "딜로포사우루스", "쌍관의 환영", "🦖"],
  [23, "iguanodon", "이구아노돈", "엄지창", "🦕"],
  [24, "kentrosaurus", "켄트로사우루스", "양날 가시", "🦕"],
  [25, "therizinosaurus", "테리지노사우루스", "월광 발톱", "🦖"],
  [26, "compsognathus", "콤프소그나투스", "그림자 질주", "🦖"],
  [27, "parasaurolophus", "파라사우롤로푸스", "울림의 파동", "🦕"],
  [28, "microraptor", "미크로랍토르", "사색 날개", "🐉"],
  [29, "brontosaurus", "브론토사우루스", "초록 거인의 발걸음", "🦕"],
  [30, "titanosaurus", "티타노사우루스", "홍염 거신", "🦕"]
].map(([artId, key, name, skill, emoji]) => Object.freeze({ artId, key, name, skill, emoji })));

export const LEGACY_SPECIES_ALIASES = Object.freeze({
  pachycephalosaurus: "dracorex",
  apatosaurus: "brontosaurus",
  diplodocus: "titanosaurus",
  argentinosaurus: "titanosaurus",
  ceratosaurus: "carnotaurus",
  styracosaurus: "triceratops",
  maiasaura: "iguanodon",
  edmontosaurus: "parasaurolophus"
});

export const EYE_COLORS = Object.freeze([
  { key: "amber", name: "호박빛", hex: "#ffbd4a" },
  { key: "emerald", name: "에메랄드", hex: "#48e0a4" },
  { key: "sky", name: "하늘빛", hex: "#67c7ff" },
  { key: "violet", name: "보랏빛", hex: "#ae82ff" },
  { key: "ruby", name: "루비", hex: "#ff647c" },
  { key: "silver", name: "은빛", hex: "#d8deea" }
]);

export const WING_COLORS = Object.freeze([
  { key: "forest", name: "숲빛", hex: "#5fcf78" },
  { key: "sunset", name: "노을빛", hex: "#ff875f" },
  { key: "ocean", name: "바다빛", hex: "#4d8dff" },
  { key: "moon", name: "달빛", hex: "#c9c7ff" },
  { key: "rose", name: "장미빛", hex: "#ff71ae" },
  { key: "obsidian", name: "흑요석", hex: "#55576a" }
]);

function clone(value) {
  return structuredClone(value);
}

function pick(items, rng) {
  return items[Math.min(items.length - 1, Math.floor(rng() * items.length))];
}

function cleanName(value, field) {
  const result = String(value ?? "").trim();
  if (result.length < 1 || result.length > 12) throw new Error(`${field} must be 1-12 characters`);
  return result;
}

export function createJourneyState() {
  return {
    version: 2,
    phase: "ONBOARDING",
    profile: null,
    starterGiftOpened: false,
    egg: null,
    dragon: null,
    wallet: 0,
    rewardedStepsToday: 0,
    debugTimeOffsetMs: 0
  };
}

export function setJourneyNames(state, { dragonName, ownerPetName }) {
  if (state.phase !== "ONBOARDING") throw new Error("onboarding already completed");
  const next = clone(state);
  next.profile = {
    dragonName: cleanName(dragonName, "dragonName"),
    ownerPetName: cleanName(ownerPetName, "ownerPetName")
  };
  next.phase = "GIFT";
  return next;
}

export function openStarterGift(state) {
  if (state.phase !== "GIFT" || state.starterGiftOpened) throw new Error("starter gift unavailable");
  const next = clone(state);
  next.starterGiftOpened = true;
  next.egg = createEgg({ id: "starter-egg" });
  next.phase = "EGG";
  return next;
}

export function applyJourneySteps(state, verifiedSteps) {
  const next = clone(state);
  if (next.phase === "EGG") {
    next.egg = addEggSteps(next.egg, verifiedSteps);
    if (next.egg.hatched) next.phase = "HATCH_READY";
    return { state: next, rewardedBigi: 0 };
  }
  if (next.phase === "HOME") {
    const reward = earnBigiFromSteps(next, verifiedSteps);
    next.wallet = reward.wallet;
    next.rewardedStepsToday = reward.rewardedStepsToday;
    return { state: next, rewardedBigi: reward.rewarded };
  }
  throw new Error("steps are not available in the current phase");
}

export function cleanJourneyEgg(state, nowMs) {
  if (state.phase !== "EGG") throw new Error("egg cannot be cleaned now");
  const next = clone(state);
  next.egg = cleanEgg(next.egg, nowMs);
  if (next.egg.hatched) next.phase = "HATCH_READY";
  return next;
}

export function hatchJourneyEgg(state, rng = Math.random) {
  if (state.phase !== "HATCH_READY") throw new Error("egg is not ready to hatch");
  const next = clone(state);
  const species = pick(SPECIES_CATALOG, rng);
  const eyeColor = pick(EYE_COLORS, rng);
  const wingColor = pick(WING_COLORS, rng);
  next.dragon = {
    ...createDragon({
      id: `starter-${species.key}`,
      name: next.profile.dragonName,
      ownerPetName: next.profile.ownerPetName,
      species: species.key
    }),
    speciesName: species.name,
    skillName: species.skill,
    emoji: species.emoji,
    artId: species.artId,
    eyeColor,
    wingColor
  };
  next.phase = "REVEAL";
  return next;
}

export function enterDragonHome(state) {
  if (state.phase !== "REVEAL") throw new Error("dragon reveal is not complete");
  const next = clone(state);
  next.phase = "HOME";
  return next;
}

export function feedJourneyDragon(state, nowMs, rng = Math.random) {
  if (state.phase !== "HOME") throw new Error("dragon is not at home");
  const result = feedDragon({ wallet: state.wallet, dragon: state.dragon }, nowMs, rng);
  const next = clone(state);
  next.wallet = result.wallet;
  next.dragon = { ...next.dragon, ...result.dragon };
  return { state: next, levelUps: result.levelUps };
}
