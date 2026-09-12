import { createBattle, createDragon, resolveRound } from "../src/game-engine.js";

const $ = (selector) => document.querySelector(selector);
const buttons = [...document.querySelectorAll("[data-direction]")];
const startButton = $("#start");
const timerNode = $("#timer");
const roleNode = $("#role");
const messageNode = $("#message");
const walletNode = $("#wallet");
const dailyNode = $("#daily-count");

let battle = null;
let countdown = null;
let seconds = 10;
let dailyCount = 0;
let myWallet = 1000;

const me = () => ({
  id: "me",
  wallet: myWallet,
  dragon: createDragon({
    id: "raon",
    name: "라온",
    ownerPetName: "대장",
    species: "tyrannosaurus",
    level: 25,
    attack: 12,
    evasion: 8,
    vitality: 10
  })
});

const opponent = () => ({
  id: "opponent",
  wallet: 1000,
  dragon: createDragon({
    id: "moru",
    name: "모루",
    ownerPetName: "친구",
    species: "triceratops",
    level: 7,
    attack: 4,
    evasion: 5,
    vitality: 2
  })
});

function renderHp(selector, hp) {
  $(selector).textContent = "♥".repeat(hp) || "전투불능";
}

function updateView() {
  walletNode.textContent = myWallet.toLocaleString("ko-KR");
  dailyNode.textContent = `${dailyCount} / 20`;
  if (!battle) {
    renderHp("#hp-a", 5);
    renderHp("#hp-b", 3);
    return;
  }
  renderHp("#hp-a", battle.players.A.hp);
  renderHp("#hp-b", battle.players.B.hp);
}

function setControls(enabled) {
  buttons.forEach((button) => { button.disabled = !enabled; });
}

function describeEvent(event) {
  const who = event.attacker === "A" ? "라온" : "모루";
  const auto = event.attackAutomatic || event.defendAutomatic ? " · 자동 플레이" : "";
  if (event.outcome === "MANUAL_DODGE") return `${who}의 공격을 방향 선택으로 피했습니다${auto}`;
  if (event.outcome === "STAT_DODGE") return `회피 능력이 발동했습니다${auto}`;
  if (event.outcome === "CRITICAL_HIT") return `${who}의 고유 스킬! 2 피해${auto}`;
  return `${who}의 공격 적중! 1 피해${auto}`;
}

function animate(event) {
  const attacker = event.attacker === "A" ? $("#fighter-a") : $("#fighter-b");
  const defender = event.defender === "A" ? $("#fighter-a") : $("#fighter-b");
  attacker.classList.add("attacking");
  if (event.damage > 0) defender.classList.add("hit");
  setTimeout(() => {
    attacker.classList.remove("attacking");
    defender.classList.remove("hit");
  }, 400);
}

function finishOrContinue(event) {
  updateView();
  animate(event);
  messageNode.textContent = describeEvent(event);
  if (battle.status === "FINISHED") {
    clearInterval(countdown);
    setControls(false);
    const won = battle.winner === "A";
    myWallet = battle.players.A.wallet;
    walletNode.textContent = myWallet.toLocaleString("ko-KR");
    roleNode.textContent = won
      ? `승리! ${battle.payout}비기 획득 · ${battle.burned}비기 소각`
      : "패배했습니다.";
    startButton.disabled = dailyCount >= 20 || myWallet < 100;
    startButton.textContent = "다시 대전하기";
    return;
  }
  beginTurn();
}

function playRound(myDirection = null) {
  clearInterval(countdown);
  setControls(false);
  const myAttacking = battle.attacker === "A";
  const choices = myAttacking
    ? { attackDirection: myDirection, defendDirection: null }
    : { attackDirection: null, defendDirection: myDirection };
  const result = resolveRound(battle, choices);
  battle = result.battle;
  finishOrContinue(result.event);
}

function beginTurn() {
  seconds = 10;
  timerNode.textContent = seconds;
  const myAttacking = battle.attacker === "A";
  roleNode.textContent = myAttacking ? "공격 방향을 선택하세요" : "회피 방향을 선택하세요";
  setControls(true);
  countdown = setInterval(() => {
    seconds -= 1;
    timerNode.textContent = seconds;
    if (seconds <= 0) playRound(null);
  }, 1000);
}

function startBattle() {
  if (dailyCount >= 20 || myWallet < 100) return;
  battle = createBattle({
    playerA: me(),
    playerB: opponent(),
    mode: "REMOTE",
    stake: 100,
    remoteBattlesTodayA: dailyCount
  });
  dailyCount += 1;
  myWallet = battle.players.A.wallet;
  startButton.disabled = true;
  startButton.textContent = "대전 진행 중";
  messageNode.textContent = "성인기 라온과 성장기 모루의 배팅 대전이 시작됐습니다.";
  updateView();
  beginTurn();
}

buttons.forEach((button) => {
  button.addEventListener("click", () => playRound(button.dataset.direction));
});
startButton.addEventListener("click", startBattle);
updateView();
