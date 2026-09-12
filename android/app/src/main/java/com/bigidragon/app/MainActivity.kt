package com.bigidragon.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.weight
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.bigidragon.app.ar.ArBattleLauncher
import com.bigidragon.app.ar.ArBattleNativeCallback
import com.bigidragon.app.ar.ArBattlePermissionPolicy
import com.bigidragon.app.ar.BleDiscoveryState
import com.bigidragon.app.ar.BleNearbyDiscovery
import com.bigidragon.app.data.BigiApiClient
import com.bigidragon.app.data.TokenVault
import com.bigidragon.app.data.BattlePlayer
import com.bigidragon.app.data.BattleState
import com.bigidragon.app.health.HealthAvailability
import com.bigidragon.app.health.HealthConnectStepReader
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val health = HealthConnectStepReader(this)
        val factory = MainViewModelFactory(BigiApiClient(), TokenVault(this), health)
        setContent {
            val viewModel: MainViewModel = viewModel(factory = factory)
            BigiDragonTheme {
                BigiDragonApp(viewModel, health)
            }
        }
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
private fun BigiDragonApp(viewModel: MainViewModel, health: HealthConnectStepReader) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val nearbyDiscovery = remember { BleNearbyDiscovery(context) }
    var pendingArBattle by remember { mutableStateOf<BattleState?>(null) }
    var pendingNearbyAction by remember { mutableStateOf<(() -> Unit)?>(null) }
    var nearbyPairingCode by remember { mutableStateOf<String?>(null) }
    var nearbyStatus by remember { mutableStateOf("대기") }
    val permissionLauncher = rememberLauncherForActivityResult(health.permissionContract()) {
        viewModel.onHealthPermissionResult(it)
    }
    val arPermissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) {
        val pending = pendingArBattle
        val nearbyAction = pendingNearbyAction
        pendingArBattle = null
        pendingNearbyAction = null
        if (pending != null && ArBattlePermissionPolicy.isGranted(context, pending.mode == "NEARBY")) {
            val result = ArBattleLauncher.launch(context, pending)
            scope.launch { snackbar.showSnackbar(result.message) }
        } else if (nearbyAction != null && ArBattlePermissionPolicy.isGranted(context, true)) {
            nearbyAction()
        } else {
            scope.launch { snackbar.showSnackbar("카메라와 근거리 기기 권한이 필요합니다.") }
        }
    }

    DisposableEffect(Unit) {
        onDispose { nearbyDiscovery.stop() }
    }

    DisposableEffect(viewModel) {
        ArBattleNativeCallback.setListener { type, payload ->
            if (type == "CHOICE") {
                runCatching { org.json.JSONObject(payload).getString("direction") }
                    .onSuccess(viewModel::submitBattleDirection)
            } else if (type == "CLOUD_ANCHOR_HOSTED") {
                runCatching { org.json.JSONObject(payload).getString("cloudAnchorId") }
                    .onSuccess(viewModel::publishCloudAnchor)
            }
        }
        onDispose { ArBattleNativeCallback.setListener(null) }
    }

    LaunchedEffect(state.battle?.id) {
        if (state.battle != null) {
            nearbyDiscovery.stop()
        }
    }

    LaunchedEffect(state.message) {
        state.message?.let {
            snackbar.showSnackbar(it)
            viewModel.clearMessage()
        }
    }

    Scaffold(
        topBar = { TopAppBar(title = { Text("비기몽", fontWeight = FontWeight.Black) }) },
        snackbarHost = { SnackbarHost(snackbar) },
        containerColor = Color(0xFFFFF8E8),
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            if (state.signedIn && state.profile != null) {
                val healthAction = {
                    if (state.healthPermissionGranted) viewModel.syncTodaySteps()
                    else permissionLauncher.launch(viewModel.healthPermissions)
                }
                when {
                    state.battle != null -> BattleScreen(
                        battle = requireNotNull(state.battle),
                        loading = state.loading,
                        onDirection = viewModel::submitBattleDirection,
                        onLeave = viewModel::leaveFinishedBattle,
                        onOpenAr = { battle ->
                            val includeNearby = battle.mode == "NEARBY"
                            if (ArBattlePermissionPolicy.isGranted(context, includeNearby)) {
                                val result = ArBattleLauncher.launch(context, battle)
                                scope.launch { snackbar.showSnackbar(result.message) }
                            } else {
                                pendingArBattle = battle
                                arPermissionLauncher.launch(ArBattlePermissionPolicy.requiredPermissions(includeNearby))
                            }
                        },
                    )
                    state.profile?.journey?.phase == "GIFT" -> StarterGiftScreen(onOpen = viewModel::openStarterGift)
                    state.profile?.journey?.phase == "EGG" -> EggScreen(
                        state = state,
                        onHealthClick = healthAction,
                        onClean = viewModel::cleanEgg,
                    )
                    state.profile?.journey?.phase == "READY_TO_HATCH" -> HatchReadyScreen(onHatch = viewModel::hatchEgg)
                    state.profile?.journey?.phase == "HATCHED" && state.showHatchReveal ->
                        DragonRevealScreen(state = state, onEnterHome = viewModel::enterDragonHome)
                    state.profile?.journey?.phase == "HATCHED" -> DragonHome(
                        state = state,
                        onHealthClick = healthAction,
                        onBattleClick = viewModel::joinRemoteBattle,
                        onCancelMatchmaking = {
                            nearbyDiscovery.stop()
                            nearbyStatus = "대기"
                            nearbyPairingCode = null
                            viewModel.cancelMatchmaking()
                        },
                        nearbyPairingCode = nearbyPairingCode,
                        nearbyStatus = nearbyStatus,
                        onNearbyHost = {
                            val action = {
                                val code = BleNearbyDiscovery.createPairingCode()
                                nearbyPairingCode = code
                                nearbyDiscovery.advertise(code) { status ->
                                    scope.launch {
                                        nearbyStatus = when (status) {
                                            BleDiscoveryState.ADVERTISING -> "상대 탐색 신호를 보내는 중"
                                            BleDiscoveryState.ERROR -> "블루투스 광고를 시작할 수 없음"
                                            else -> status.name
                                        }
                                    }
                                }
                                viewModel.joinNearbyBattle(code)
                            }
                            if (ArBattlePermissionPolicy.isGranted(context, true)) action()
                            else {
                                pendingNearbyAction = action
                                arPermissionLauncher.launch(ArBattlePermissionPolicy.requiredPermissions(true))
                            }
                        },
                        onNearbyScan = {
                            val action = {
                                nearbyStatus = "근처 비기몽을 찾는 중"
                                nearbyDiscovery.scan(
                                    onPairingCode = { code ->
                                        scope.launch {
                                            nearbyDiscovery.stop()
                                            nearbyPairingCode = code
                                            nearbyStatus = "상대를 찾음 · 서버 확인 중"
                                            viewModel.joinNearbyBattle(code)
                                        }
                                    },
                                    onState = { status ->
                                        scope.launch {
                                            if (status == BleDiscoveryState.ERROR) nearbyStatus = "블루투스 탐색 오류"
                                        }
                                    },
                                )
                            }
                            if (ArBattlePermissionPolicy.isGranted(context, true)) action()
                            else {
                                pendingNearbyAction = action
                                arPermissionLauncher.launch(ArBattlePermissionPolicy.requiredPermissions(true))
                            }
                        },
                    )
                    else -> Text("모험 상태를 불러오는 중이에요.", modifier = Modifier.padding(24.dp))
                }
            } else {
                AccountOnboarding(loading = state.loading, onCreate = viewModel::createGuest)
            }
            if (state.loading) {
                Box(
                    Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.15f)),
                    contentAlignment = Alignment.Center,
                ) { CircularProgressIndicator(color = Color(0xFF7B4B1E)) }
            }
        }
    }
}

@Composable
private fun AccountOnboarding(loading: Boolean, onCreate: (String, String) -> Unit) {
    var dragonName by remember { mutableStateOf("") }
    var ownerPetName by remember { mutableStateOf("") }
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
    ) {
        Text("첫 만남을 준비해요", fontSize = 28.sp, fontWeight = FontWeight.Black)
        Spacer(Modifier.height(8.dp))
        Text("공룡에게 붙일 이름과 공룡이 나를 부를 애칭은 서로 달라요.", color = Color(0xFF62584B))
        Spacer(Modifier.height(24.dp))
        OutlinedTextField(
            value = dragonName,
            onValueChange = { if (it.length <= 12) dragonName = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("공룡 이름") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Next),
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = ownerPetName,
            onValueChange = { if (it.length <= 12) ownerPetName = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("공룡이 나를 부를 애칭") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
        )
        Spacer(Modifier.height(20.dp))
        Button(
            onClick = { onCreate(dragonName, ownerPetName) },
            enabled = !loading && dragonName.isNotBlank() && ownerPetName.isNotBlank(),
            modifier = Modifier.fillMaxWidth().height(52.dp),
        ) { Text("모험 시작하기") }
    }
}

@Composable
private fun StarterGiftScreen(onOpen: () -> Unit) {
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("첫 선물이 도착했어요", fontSize = 28.sp, fontWeight = FontWeight.Black)
        Spacer(Modifier.height(18.dp))
        Text("🎁", fontSize = 120.sp)
        Spacer(Modifier.height(18.dp))
        Text("상자 안에는 함께 걸으며 부화시킬 공룡알이 들어 있어요.", color = Color(0xFF62584B))
        Spacer(Modifier.height(24.dp))
        Button(onClick = onOpen, modifier = Modifier.fillMaxWidth().height(54.dp)) {
            Text("선물상자 열기")
        }
    }
}

@Composable
private fun EggScreen(state: MainUiState, onHealthClick: () -> Unit, onClean: () -> Unit) {
    val egg = requireNotNull(state.profile?.journey?.egg)
    val canClean = egg.nextCleanAt == null || egg.nextCleanAt <= System.currentTimeMillis()
    val percent = (egg.progress * 100 / egg.required).coerceIn(0, 100)
    Column(
        Modifier.fillMaxSize().padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("알과 함께 걸어주세요", fontSize = 26.sp, fontWeight = FontWeight.Black)
        Spacer(Modifier.height(4.dp))
        Text("🥚", fontSize = 124.sp)
        Card(Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp)) {
            Column(Modifier.padding(18.dp)) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text("부화 게이지", fontWeight = FontWeight.Bold)
                    Text("$percent%")
                }
                Spacer(Modifier.height(10.dp))
                LinearProgressIndicator(
                    progress = { egg.progress / egg.required.toFloat() },
                    modifier = Modifier.fillMaxWidth().height(14.dp),
                )
                Spacer(Modifier.height(8.dp))
                Text("${egg.progress} / ${egg.required} 포인트", color = Color(0xFF62584B))
            }
        }
        Button(
            onClick = onHealthClick,
            enabled = state.healthAvailability == HealthAvailability.AVAILABLE && !state.loading,
            modifier = Modifier.fillMaxWidth().height(50.dp),
        ) { Text(if (state.healthPermissionGranted) "오늘 걸음 채우기" else "걸음 수 권한 연결") }
        OutlinedButton(
            onClick = onClean,
            enabled = canClean && !state.loading,
            modifier = Modifier.fillMaxWidth().height(50.dp),
        ) { Text(if (canClean) "알 닦기 · +500" else cleanCooldownLabel(egg.nextCleanAt)) }
        Text(
            if (state.healthPermissionGranted) "실제 1걸음은 부화 포인트 1로 반영돼요."
            else "Health Connect를 연결하면 실제 걸음으로 게이지를 채울 수 있어요.",
            color = Color(0xFF776B5E),
        )
    }
}

@Composable
private fun HatchReadyScreen(onHatch: () -> Unit) {
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("알이 움직이기 시작했어요!", fontSize = 28.sp, fontWeight = FontWeight.Black)
        Spacer(Modifier.height(18.dp))
        Text("✨🥚✨", fontSize = 88.sp)
        Spacer(Modifier.height(12.dp))
        Text("30,000포인트 달성", color = Color(0xFF2F7D4A), fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(28.dp))
        Button(onClick = onHatch, modifier = Modifier.fillMaxWidth().height(56.dp)) {
            Text("알 부화시키기")
        }
    }
}

@Composable
private fun DragonRevealScreen(state: MainUiState, onEnterHome: () -> Unit) {
    val dragon = requireNotNull(state.profile?.dragon)
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("새로운 가족이 태어났어요!", fontSize = 27.sp, fontWeight = FontWeight.Black)
        Spacer(Modifier.height(16.dp))
        CharacterArtwork(
            artId = dragon.artId,
            stage = dragon.stage,
            fallbackEmoji = dragon.emoji,
            modifier = Modifier.size(220.dp),
        )
        Text(dragon.name, fontSize = 30.sp, fontWeight = FontWeight.Black)
        Text("${dragon.speciesName} 모티브 드래곤", color = Color(0xFF62584B))
        Spacer(Modifier.height(18.dp))
        Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFFFE3A3))) {
            Column(Modifier.padding(18.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                Text("눈 ${dragon.eyeColorName} · 날개 ${dragon.wingColorName}")
                Text("고유 기술 · ${dragon.skillName}", fontWeight = FontWeight.Bold)
            }
        }
        Spacer(Modifier.height(26.dp))
        Button(onClick = onEnterHome, modifier = Modifier.fillMaxWidth().height(54.dp)) {
            Text("${dragon.name}와 시작하기")
        }
    }
}

@Composable
private fun DragonHome(
    state: MainUiState,
    onHealthClick: () -> Unit,
    onBattleClick: (Int) -> Unit,
    onCancelMatchmaking: () -> Unit,
    nearbyPairingCode: String?,
    nearbyStatus: String,
    onNearbyHost: () -> Unit,
    onNearbyScan: () -> Unit,
) {
    val profile = requireNotNull(state.profile)
    val dragon = requireNotNull(profile.dragon)
    Column(
        Modifier.fillMaxSize().padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = Color(0xFFFFE3A3)),
            shape = RoundedCornerShape(24.dp),
        ) {
            Column(
                Modifier.fillMaxWidth().padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                CharacterArtwork(
                    artId = dragon.artId,
                    stage = dragon.stage,
                    fallbackEmoji = dragon.emoji,
                    modifier = Modifier.size(150.dp),
                )
                Text(dragon.name, fontSize = 26.sp, fontWeight = FontWeight.Black)
                Text("Lv.${dragon.level} · ${stageLabel(dragon.stage)} · ${dragon.speciesName}")
                Spacer(Modifier.height(10.dp))
                Text("“${dragon.ownerPetName}, 오늘도 같이 걸을까?”", color = Color(0xFF6A4618))
            }
        }

        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            StatCard("비기", profile.wallet.toString(), Modifier.weight(1f))
            StatCard("오늘 걸음", state.todaySteps.toString(), Modifier.weight(1f))
            StatCard("체력", dragon.vitality.toString(), Modifier.weight(1f))
        }

        Card(Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp)) {
            Column(Modifier.padding(18.dp)) {
                Text("Health Connect", fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(8.dp))
                LinearProgressIndicator(
                    progress = { (state.todaySteps.coerceAtMost(10_000) / 10_000f) },
                    modifier = Modifier.fillMaxWidth().height(10.dp),
                )
                Spacer(Modifier.height(8.dp))
                Text(healthMessage(state), color = Color(0xFF62584B))
                Spacer(Modifier.height(12.dp))
                Button(
                    onClick = onHealthClick,
                    enabled = state.healthAvailability == HealthAvailability.AVAILABLE && !state.loading,
                    modifier = Modifier.fillMaxWidth(),
                ) { Text(if (state.healthPermissionGranted) "오늘 걸음 동기화" else "걸음 수 권한 연결") }
            }
        }

        Card(Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp)) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text("원격 서버대전", fontWeight = FontWeight.Bold)
                if (state.matchmaking && state.matchmakingMode == "REMOTE") {
                    Text("${state.matchmakingStake}비기 방에서 상대를 찾는 중…")
                    OutlinedButton(onClick = onCancelMatchmaking, modifier = Modifier.fillMaxWidth()) {
                        Text("매칭 취소")
                    }
                } else {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf(0, 10, 50, 100).forEach { stake ->
                            OutlinedButton(
                                onClick = { onBattleClick(stake) },
                                enabled = !state.loading && !state.matchmaking && profile.wallet >= stake,
                                modifier = Modifier.weight(1f),
                            ) { Text(if (stake == 0) "연습" else "$stake") }
                        }
                    }
                    Text("유료 서버대전은 하루 최대 20회", fontSize = 12.sp, color = Color(0xFF776B5E))
                }
            }
        }

        Card(Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp)) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text("근거리 블루투스 AR 대전", fontWeight = FontWeight.Bold)
                Text("두 기기가 서로를 찾으면 1,000비기를 서버에 예치하고 카메라 AR 전투로 전환합니다.", fontSize = 12.sp, color = Color(0xFF776B5E))
                if (state.matchmaking && state.matchmakingMode == "NEARBY") {
                    Text(nearbyPairingCode?.let { "연결 코드 $it" } ?: "연결 코드 확인 중", fontWeight = FontWeight.Bold)
                    Text(nearbyStatus, fontSize = 12.sp)
                    OutlinedButton(onClick = onCancelMatchmaking, modifier = Modifier.fillMaxWidth()) { Text("근거리 매칭 취소") }
                } else {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(
                            onClick = onNearbyHost,
                            enabled = !state.loading && !state.matchmaking && profile.wallet >= 1_000,
                            modifier = Modifier.weight(1f),
                        ) { Text("대전 열기") }
                        OutlinedButton(
                            onClick = onNearbyScan,
                            enabled = !state.loading && !state.matchmaking && profile.wallet >= 1_000,
                            modifier = Modifier.weight(1f),
                        ) { Text("근처 대전 찾기") }
                    }
                    if (profile.wallet < 1_000) Text("근거리 대전에는 1,000비기가 필요해요.", fontSize = 12.sp, color = Color(0xFFC24A3A))
                }
            }
        }
    }
}

@Composable
private fun BattleScreen(
    battle: BattleState,
    loading: Boolean,
    onDirection: (String) -> Unit,
    onLeave: () -> Unit,
    onOpenAr: (BattleState) -> Unit,
) {
    val you = if (battle.youAre == "A") battle.playerA else battle.playerB
    val opponent = if (battle.youAre == "A") battle.playerB else battle.playerA
    val last = battle.history.lastOrNull()
    val isYourAttack = battle.attacker == battle.youAre
    val secondsLeft by produceState(initialValue = 0, battle.deadlineAt, battle.round, battle.status) {
        while (battle.status == "ACTIVE") {
            value = (((battle.deadlineAt - System.currentTimeMillis()).coerceAtLeast(0) + 999) / 1000).toInt()
            delay(200)
        }
    }

    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text("ROUND ${battle.round}", fontSize = 22.sp, fontWeight = FontWeight.Black)
            Text(if (battle.stake == 0) "연습전" else "${battle.stake}비기 배팅", fontWeight = FontWeight.Bold)
        }
        Button(onClick = { onOpenAr(battle) }, modifier = Modifier.fillMaxWidth().height(52.dp)) {
            Text("카메라로 AR 대전 보기")
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            BattlePlayerCard("나", you, battle.youAre, last?.let {
                if (it.attacker == battle.youAre) it.motion.attacker else it.motion.defender
            }, Modifier.weight(1f))
            BattlePlayerCard("상대", opponent, if (battle.youAre == "A") "B" else "A", last?.let {
                if (it.attacker == battle.youAre) it.motion.defender else it.motion.attacker
            }, Modifier.weight(1f))
        }

        if (battle.status == "ACTIVE") {
            Card(
                Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = if (secondsLeft <= 3) Color(0xFFFFD4CF) else Color(0xFFFFE3A3)),
            ) {
                Column(Modifier.fillMaxWidth().padding(16.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(if (isYourAttack) "공격 방향을 선택하세요" else "피할 방향을 선택하세요", fontWeight = FontWeight.Bold)
                    Text("${secondsLeft}초", fontSize = 38.sp, fontWeight = FontWeight.Black)
                    Text(
                        if (battle.choiceSubmitted) "선택 완료 · 상대를 기다리는 중"
                        else "시간이 끝나면 서버가 자동 선택해요",
                        color = Color(0xFF62584B),
                    )
                }
            }
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("LEFT" to "왼쪽", "CENTER" to "가운데", "RIGHT" to "오른쪽").forEach { (direction, label) ->
                    Button(
                        onClick = { onDirection(direction) },
                        enabled = !loading && !battle.choiceSubmitted && secondsLeft > 0,
                        modifier = Modifier.weight(1f).height(52.dp),
                    ) { Text(label) }
                }
            }
        } else {
            val won = battle.winner == battle.youAre
            Card(
                Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = if (won) Color(0xFFD7F4DE) else Color(0xFFFFDAD6)),
            ) {
                Column(Modifier.fillMaxWidth().padding(20.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(if (won) "승리!" else "패배", fontSize = 34.sp, fontWeight = FontWeight.Black)
                    if (battle.stake > 0) Text("승자 지급 ${battle.payout}비기 · 소각 ${battle.burned}비기")
                    Spacer(Modifier.height(12.dp))
                    Button(onClick = onLeave, modifier = Modifier.fillMaxWidth()) { Text("홈으로 돌아가기") }
                }
            }
        }

        last?.let { event ->
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp)) {
                    Text("최근 판정 · ${battleOutcomeLabel(event.outcome)}", fontWeight = FontWeight.Bold)
                    Text("공격 ${directionLabel(event.attackDirection)} / 회피 ${directionLabel(event.defendDirection)}")
                    if (event.attackAutomatic || event.defendAutomatic) Text("⏱ 시간 초과 자동 플레이 포함", color = Color(0xFFC24A3A))
                    Text("모션: ${event.motion.attacker} → ${event.motion.defender}", fontSize = 11.sp, color = Color(0xFF776B5E))
                }
            }
        }
    }
}

@Composable
private fun BattlePlayerCard(
    label: String,
    player: BattlePlayer,
    side: String,
    motionKey: String?,
    modifier: Modifier = Modifier,
) {
    val maxHp = healthForBattle(player.dragon.vitality)
    Card(modifier, shape = RoundedCornerShape(18.dp)) {
        Column(Modifier.padding(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text("$label · $side", fontWeight = FontWeight.Bold)
            CharacterArtwork(
                artId = player.dragon.artId,
                stage = player.dragon.stage,
                fallbackEmoji = speciesEmoji(player.dragon.species),
                modifier = Modifier.size(92.dp),
            )
            Text(player.dragon.name, fontWeight = FontWeight.Bold)
            Text("Lv.${player.dragon.level} · ${stageLabel(player.dragon.stage)}", fontSize = 12.sp)
            Spacer(Modifier.height(8.dp))
            LinearProgressIndicator(
                progress = { player.hp / maxHp.toFloat() },
                modifier = Modifier.fillMaxWidth().height(8.dp),
            )
            Text("HP ${player.hp} / $maxHp", fontSize = 12.sp)
            if (motionKey != null) Text(motionKey, fontSize = 9.sp, color = Color(0xFF88796B))
        }
    }
}

private fun battleOutcomeLabel(outcome: String): String = when (outcome) {
    "HIT" -> "공격 적중"
    "CRITICAL_HIT" -> "강타 적중"
    "MANUAL_DODGE" -> "방향 회피 성공"
    "STAT_DODGE" -> "능력치 자동 회피"
    else -> outcome
}

private fun directionLabel(direction: String): String = when (direction) {
    "LEFT" -> "왼쪽"
    "CENTER" -> "가운데"
    "RIGHT" -> "오른쪽"
    else -> direction
}

private fun healthForBattle(vitality: Int): Int = when {
    vitality >= 10 -> 5
    vitality >= 5 -> 4
    else -> 3
}

private fun speciesEmoji(species: String): String = when (species) {
    "pterosaur" -> "🐉"
    "tyrannosaurus", "spinosaurus" -> "🐲"
    "brachiosaurus", "stegosaurus", "ankylosaurus" -> "🦕"
    else -> "🦖"
}

@Composable
private fun StatCard(label: String, value: String, modifier: Modifier = Modifier) {
    Card(modifier, shape = RoundedCornerShape(16.dp)) {
        Column(Modifier.padding(12.dp)) {
            Text(label, fontSize = 12.sp, color = Color(0xFF776B5E))
            Text(value, fontSize = 20.sp, fontWeight = FontWeight.Bold)
        }
    }
}

private fun stageLabel(stage: String): String = when (stage) {
    "GROWTH" -> "성장기"
    "YOUTH" -> "청년기"
    "ADULT" -> "성인기"
    else -> stage
}

private fun cleanCooldownLabel(nextCleanAt: Long?): String {
    if (nextCleanAt == null) return "알 닦기 · +500"
    val remainingMinutes = ((nextCleanAt - System.currentTimeMillis()).coerceAtLeast(0) + 59_999) / 60_000
    val hours = remainingMinutes / 60
    val minutes = remainingMinutes % 60
    return if (hours > 0) "${hours}시간 ${minutes}분 후 닦기" else "${minutes}분 후 닦기"
}

private fun healthMessage(state: MainUiState): String = when (state.healthAvailability) {
    HealthAvailability.UNAVAILABLE -> "이 기기에서는 Health Connect를 사용할 수 없어요."
    HealthAvailability.UPDATE_REQUIRED -> "Health Connect 설치 또는 업데이트가 필요해요."
    HealthAvailability.AVAILABLE -> if (state.healthPermissionGranted) {
        "누적 걸음 중 새로 증가한 만큼만 하루 최대 10,000비기로 바뀝니다."
    } else {
        "걸음 수 읽기 권한을 연결하면 비기를 받을 수 있어요."
    }
}

@Composable
private fun BigiDragonTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MaterialTheme.colorScheme.copy(
            primary = Color(0xFF7B4B1E),
            secondary = Color(0xFF2F7D4A),
            background = Color(0xFFFFF8E8),
        ),
        content = content,
    )
}
