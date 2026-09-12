import { RULES, xpForNextLevel } from "../src/game-engine.js";
import {
  applyJourneySteps,
  cleanJourneyEgg,
  createJourneyState,
  enterDragonHome,
  feedJourneyDragon,
  hatchJourneyEgg,
  openStarterGift,
  setJourneyNames
} from "../src/journey-engine.js";

const STORAGE_KEY = "bigi-dragon-journey-v2";
const screen = document.querySelector("#screen");
let state = loadState();

function loadState() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY)) ?? createJourneyState();
  } catch {
    return createJourneyState();
  }
}

function saveState() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function commit(next) {
  state = next;
  saveState();
  render();
}

function currentTime() {
  return Date.now() + state.debugTimeOffsetMs;
}

function shell(content) {
  screen.className = "screen";
  screen.innerHTML = content;
}

function bind(selector, event, handler) {
  document.querySelector(selector)?.addEventListener(event, handler);
}

function showError(message) {
  const node = document.querySelector("#error");
  if (node) node.textContent = message;
}

function renderOnboarding() {
  shell(`
    <p class="eyebrow">우리의 첫 만남</p>
    <h1>어떻게 부르면 될까?</h1>
    <p>공룡의 이름과 공룡이 나를 부를 애칭을 정해주세요.</p>
    <form class="form" id="names-form">
      <label>공룡 이름 <small>컬렉션과 대전에 표시됩니다.</small><input name="dragonName" maxlength="12" placeholder="예: 라온" required></label>
      <label>나의 애칭 <small>공룡만 나를 이렇게 부릅니다.</small><input name="ownerPetName" maxlength="12" placeholder="예: 대장" required></label>
      <p class="error" id="error"></p>
      <button class="primary" type="submit">이름 정하기</button>
    </form>
  `);
  bind("#names-form", "submit", (event) => {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    try {
      commit(setJourneyNames(state, {
        dragonName: values.get("dragonName"),
        ownerPetName: values.get("ownerPetName")
      }));
    } catch (error) { showError(error.message); }
  });
}

function renderGift() {
  shell(`
    <p class="eyebrow">첫 번째 선물</p>
    <div class="gift" aria-hidden="true">🎁</div>
    <h1>${state.profile.ownerPetName}, 선물이 도착했어!</h1>
    <p>상자를 열면 함께 성장할 특별한 공룡알을 받을 수 있어요.</p>
    <button class="primary" id="open-gift" type="button">선물상자 열기</button>
  `);
  bind("#open-gift", "click", () => commit(openStarterGift(state)));
}

function renderEgg() {
  const progress = state.egg.progress;
  const percent = Math.min(100, Math.round(progress / RULES.eggProgressRequired * 100));
  const now = currentTime();
  const cleanRemaining = state.egg.lastCleanedAt === null
    ? 0
    : Math.max(0, RULES.eggCleanCooldownMs - (now - state.egg.lastCleanedAt));
  const cleanText = cleanRemaining === 0 ? "알 닦기 +500" : `다시 닦기 ${Math.ceil(cleanRemaining / 60000)}분`;
  shell(`
    <p class="eyebrow">부화까지 ${RULES.eggProgressRequired - progress}포인트</p>
    <div class="egg" aria-hidden="true">🥚</div>
    <h1>${state.profile.dragonName}의 알</h1>
    <p>${state.profile.ownerPetName}, 조금만 더 걸으면 만날 수 있어!</p>
    <div class="progress-wrap">
      <div class="progress-label"><span>부화 게이지</span><strong>${progress.toLocaleString()} / 30,000</strong></div>
      <div class="progress" role="progressbar" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="30000"><div style="width:${percent}%"></div></div>
    </div>
    <div class="button-grid">
      <button class="secondary" id="walk-1000" type="button">테스트 걸음 +1,000</button>
      <button class="secondary" id="walk-5000" type="button">빠른 테스트 +5,000</button>
      <button class="primary wide" id="clean" type="button" ${cleanRemaining > 0 ? "disabled" : ""}>${cleanText}</button>
      ${cleanRemaining > 0 ? '<button class="secondary wide" id="skip-time" type="button">개발용: 2시간 건너뛰기</button>' : ""}
    </div>
    <p class="hint">개발 실험판의 버튼입니다. 실제 앱에서는 스마트폰의 검증된 걸음 수가 자동 반영됩니다.</p>
    <p class="error" id="error"></p>
  `);
  bind("#walk-1000", "click", () => commit(applyJourneySteps(state, 1000).state));
  bind("#walk-5000", "click", () => commit(applyJourneySteps(state, 5000).state));
  bind("#clean", "click", () => {
    try { commit(cleanJourneyEgg(state, currentTime())); }
    catch (error) { showError(error.message); }
  });
  bind("#skip-time", "click", () => {
    const next = structuredClone(state);
    next.debugTimeOffsetMs += RULES.eggCleanCooldownMs;
    commit(next);
  });
}

function renderHatchReady() {
  shell(`
    <p class="eyebrow">부화 준비 완료</p>
    <div class="egg" aria-hidden="true">✨🥚✨</div>
    <h1>알에서 빛이 나고 있어!</h1>
    <p>30종 중 어떤 드래곤이 기다리고 있을까요?</p>
    <button class="primary" id="hatch" type="button">${state.profile.dragonName} 만나기</button>
  `);
  bind("#hatch", "click", () => commit(hatchJourneyEgg(state)));
}

function renderReveal() {
  const dragon = state.dragon;
  shell(`
    <p class="eyebrow">새로운 드래곤 탄생</p>
    <div class="reveal-card" style="--wing:${dragon.wingColor.hex}">
      <div class="dragon-visual" aria-hidden="true">${dragon.emoji}</div>
      <h1>${dragon.name}</h1>
      <p>${dragon.speciesName}를 모티브로 태어난 성장기 드래곤</p>
      <p><strong>고유 스킬 · ${dragon.skillName}</strong></p>
      <div class="colors">
        <span class="color-chip"><i class="swatch" style="background:${dragon.eyeColor.hex}"></i>눈 ${dragon.eyeColor.name}</span>
        <span class="color-chip"><i class="swatch" style="background:${dragon.wingColor.hex}"></i>날개 ${dragon.wingColor.name}</span>
      </div>
    </div>
    <button class="primary" id="enter-home" type="button">함께 시작하기</button>
  `);
  bind("#enter-home", "click", () => commit(enterDragonHome(state)));
}

function renderHome() {
  const dragon = state.dragon;
  const nextXp = xpForNextLevel(dragon.level);
  shell(`
    <div class="home-header"><div><p class="eyebrow">오늘의 동행</p><h1>${dragon.name}</h1></div><span class="coin">◆ ${state.wallet.toLocaleString()} 비기</span></div>
    <div class="home-card">
      <div class="dragon-visual" aria-hidden="true">${dragon.emoji}</div>
      <p>${dragon.speciesName} · Lv.${dragon.level} ${dragon.stage === "GROWTH" ? "성장기" : dragon.stage === "YOUTH" ? "청년기" : "성인기"}</p>
      <p><strong>${dragon.skillName}</strong></p>
    </div>
    <p class="speech">“${dragon.ownerPetName}, 오늘도 같이 걸어볼까?”</p>
    <div class="progress-wrap">
      <div class="progress-label"><span>경험치</span><strong>${dragon.experience} / ${nextXp ?? "MAX"}</strong></div>
      <div class="progress"><div style="width:${nextXp ? Math.round(dragon.experience / nextXp * 100) : 100}%"></div></div>
    </div>
    <div class="stats">
      <span>공격<strong>${dragon.stats.attack}</strong></span>
      <span>회피<strong>${dragon.stats.evasion}</strong></span>
      <span>체력<strong>${dragon.stats.vitality}</strong></span>
    </div>
    <div class="button-grid">
      <button class="secondary" id="home-walk" type="button">테스트 걸음 +1,000</button>
      <button class="secondary" id="home-walk-fast" type="button">빠른 테스트 +5,000</button>
      <button class="primary" id="feed" type="button">밥 주기 -500</button>
      <button class="secondary" id="skip-feed-time" type="button">개발용: 1시간 이동</button>
    </div>
    <p class="hint">오늘 걸음 보상 ${state.rewardedStepsToday.toLocaleString()} / 10,000비기</p>
    <p class="error" id="error"></p>
    <a class="link" href="./index.html">서버대전 실험판으로 이동 →</a>
  `);
  bind("#home-walk", "click", () => {
    const result = applyJourneySteps(state, 1000);
    commit(result.state);
  });
  bind("#home-walk-fast", "click", () => {
    const result = applyJourneySteps(state, 5000);
    commit(result.state);
  });
  bind("#feed", "click", () => {
    try {
      const result = feedJourneyDragon(state, currentTime());
      commit(result.state);
    } catch (error) { showError(error.message); }
  });
  bind("#skip-feed-time", "click", () => {
    const next = structuredClone(state);
    next.debugTimeOffsetMs += RULES.feedCooldownMs;
    commit(next);
  });
}

function render() {
  const views = {
    ONBOARDING: renderOnboarding,
    GIFT: renderGift,
    EGG: renderEgg,
    HATCH_READY: renderHatchReady,
    REVEAL: renderReveal,
    HOME: renderHome
  };
  (views[state.phase] ?? renderOnboarding)();
}

document.querySelector("#reset").addEventListener("click", () => {
  if (window.confirm("저장된 테스트 진행 상황을 모두 초기화할까요?")) commit(createJourneyState());
});
render();
