package com.bigidragon.app

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.bigidragon.app.data.BigiApiClient
import com.bigidragon.app.data.BattleState
import com.bigidragon.app.data.TokenVault
import com.bigidragon.app.data.UserProfile
import com.bigidragon.app.health.HealthAvailability
import com.bigidragon.app.health.HealthConnectStepReader
import com.bigidragon.app.ar.ArBattlePayloadFactory
import com.bigidragon.app.ar.UnityBattleMessenger
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

data class MainUiState(
    val loading: Boolean = false,
    val signedIn: Boolean = false,
    val profile: UserProfile? = null,
    val healthAvailability: HealthAvailability = HealthAvailability.UNAVAILABLE,
    val healthPermissionGranted: Boolean = false,
    val todaySteps: Long = 0,
    val showHatchReveal: Boolean = false,
    val matchmaking: Boolean = false,
    val matchmakingMode: String? = null,
    val matchmakingStake: Int = 0,
    val battle: BattleState? = null,
    val battleConnectionLost: Boolean = false,
    val message: String? = null,
)

class MainViewModel(
    private val api: BigiApiClient,
    private val tokenVault: TokenVault,
    private val health: HealthConnectStepReader,
) : ViewModel() {
    private val mutableState = MutableStateFlow(MainUiState())
    val state: StateFlow<MainUiState> = mutableState.asStateFlow()
    val healthPermissions: Set<String> get() = health.permissions
    private var matchmakingJob: Job? = null
    private var battleJob: Job? = null
    private var lastUnityBattleId: String? = null
    private var lastUnityRoundSent = 0

    init {
        refreshSession()
    }

    fun createGuest(dragonName: String, ownerPetName: String) {
        if (dragonName.isBlank() || ownerPetName.isBlank()) {
            mutableState.update { it.copy(message = "공룡 이름과 나를 부를 애칭을 모두 입력해주세요.") }
            return
        }
        launchBusy {
            val account = api.createGuest(dragonName.trim(), ownerPetName.trim())
            tokenVault.save(account.token)
            mutableState.update {
                it.copy(signedIn = true, profile = account.user, message = "${dragonName.trim()}의 선물이 도착했어요!")
            }
            refreshHealthPermission()
        }
    }

    fun openStarterGift() {
        val token = tokenVault.load() ?: return
        launchBusy {
            val profile = api.openStarterGift(token)
            mutableState.update { it.copy(profile = profile, message = "공룡알을 선물받았어요!") }
            refreshHealthPermission()
        }
    }

    fun cleanEgg() {
        val token = tokenVault.load() ?: return
        launchBusy {
            val profile = api.cleanEgg(token)
            mutableState.update { it.copy(profile = profile, message = "알을 닦아 500포인트가 올랐어요.") }
        }
    }

    fun hatchEgg() {
        val token = tokenVault.load() ?: return
        launchBusy {
            val profile = api.hatchEgg(token)
            mutableState.update { it.copy(profile = profile, showHatchReveal = true) }
        }
    }

    fun enterDragonHome() {
        mutableState.update { it.copy(showHatchReveal = false) }
    }

    fun onHealthPermissionResult(granted: Set<String>) {
        val allowed = granted.containsAll(health.permissions)
        mutableState.update { it.copy(healthPermissionGranted = allowed) }
        if (allowed) syncTodaySteps()
        else mutableState.update { it.copy(message = "걸음 수 권한이 허용되지 않았어요.") }
    }

    fun syncTodaySteps() {
        val token = tokenVault.load() ?: return
        launchBusy {
            val total = health.readTodaySteps()
            val result = api.syncSteps(token, health.todayKst(), total)
            val profile = api.getMe(token)
            mutableState.update {
                it.copy(
                    profile = profile,
                    todaySteps = total,
                    message = when {
                        result.eggProgressAdded > 0 && profile.journey.phase == "READY_TO_HATCH" ->
                            "알의 게이지가 가득 찼어요!"
                        result.eggProgressAdded > 0 ->
                            "새 걸음 ${result.eggProgressAdded}보가 알에 채워졌어요."
                        result.rewarded > 0 ->
                            "새 걸음 ${result.rewarded}보를 ${result.rewarded}비기로 바꿨어요."
                        else -> "오늘 걸음은 모두 반영되어 있어요."
                    },
                )
            }
        }
    }

    fun joinRemoteBattle(stake: Int) {
        val token = tokenVault.load() ?: return
        launchBusy {
            val result = api.joinRemoteBattle(token, stake)
            if (result.battle != null) {
                mutableState.update { it.copy(matchmaking = false, matchmakingMode = null, battle = result.battle, message = "대전 상대를 찾았어요!") }
                startBattlePolling()
            } else {
                mutableState.update { it.copy(matchmaking = true, matchmakingMode = "REMOTE", matchmakingStake = stake, message = "대전 상대를 찾는 중이에요.") }
                startMatchmakingPolling()
            }
        }
    }

    fun joinNearbyBattle(pairingCode: String) {
        val token = tokenVault.load() ?: return
        launchBusy {
            val result = api.joinNearbyBattle(token, pairingCode)
            if (result.battle != null) {
                mutableState.update { it.copy(matchmaking = false, matchmakingMode = null, battle = result.battle, message = "근거리 AR 대전이 연결됐어요!") }
                startBattlePolling()
            } else {
                mutableState.update {
                    it.copy(matchmaking = true, matchmakingMode = "NEARBY", matchmakingStake = 1_000, message = "근처 상대의 연결을 기다리는 중이에요.")
                }
                startMatchmakingPolling()
            }
        }
    }

    fun cancelMatchmaking() {
        val token = tokenVault.load() ?: return
        launchBusy {
            api.cancelMatchmaking(token)
            matchmakingJob?.cancel()
            mutableState.update { it.copy(matchmaking = false, matchmakingMode = null, message = "매칭을 취소했어요.") }
        }
    }

    fun submitBattleDirection(direction: String) {
        val token = tokenVault.load() ?: return
        val battleId = mutableState.value.battle?.id ?: return
        launchBusy {
            val battle = api.submitBattleChoice(token, battleId, direction)
            mutableState.update { it.copy(battle = battle) }
            syncUnityBattle(battle)
            if (battle.status == "ACTIVE") startBattlePolling()
        }
    }

    fun publishCloudAnchor(cloudAnchorId: String) {
        val token = tokenVault.load() ?: return
        val battleId = mutableState.value.battle?.id ?: return
        viewModelScope.launch {
            runCatching { api.publishCloudAnchor(token, battleId, cloudAnchorId) }
                .onFailure { mutableState.update { state -> state.copy(message = "공유 AR 위치를 서버에 등록하지 못했어요.") } }
        }
    }

    fun leaveFinishedBattle() {
        if (mutableState.value.battle?.status != "FINISHED") return
        battleJob?.cancel()
        mutableState.update { it.copy(battle = null) }
        refreshProfile()
    }

    fun clearMessage() {
        mutableState.update { it.copy(message = null) }
    }

    private fun refreshSession() {
        val token = tokenVault.load()
        mutableState.update { it.copy(healthAvailability = health.availability()) }
        if (token == null) return
        launchBusy {
            runCatching { api.getMe(token) }
                .onSuccess { profile ->
                    mutableState.update { it.copy(signedIn = true, profile = profile) }
                    restoreBattleOrQueue(token)
                }
                .onFailure {
                    tokenVault.clear()
                    mutableState.update { state -> state.copy(signedIn = false, profile = null, message = "다시 시작해주세요.") }
                }
            refreshHealthPermission()
        }
    }

    private suspend fun refreshHealthPermission() {
        mutableState.update {
            it.copy(
                healthAvailability = health.availability(),
                healthPermissionGranted = health.hasPermission(),
            )
        }
    }

    private fun restoreBattleOrQueue(token: String) {
        viewModelScope.launch {
            runCatching { api.getMatchmaking(token) }.onSuccess { result ->
                when {
                    result.battle != null -> {
                        mutableState.update { it.copy(battle = result.battle, matchmaking = false, matchmakingMode = null) }
                        startBattlePolling()
                    }
                    result.status == "QUEUED" -> {
                        mutableState.update { it.copy(matchmaking = true, matchmakingMode = result.mode, matchmakingStake = result.stake) }
                        startMatchmakingPolling()
                    }
                }
            }
        }
    }

    private fun startMatchmakingPolling() {
        matchmakingJob?.cancel()
        val token = tokenVault.load() ?: return
        matchmakingJob = viewModelScope.launch {
            while (isActive && mutableState.value.matchmaking) {
                delay(1_000)
                runCatching { api.getMatchmaking(token) }.onSuccess { result ->
                    if (result.battle != null) {
                        mutableState.update { it.copy(matchmaking = false, matchmakingMode = null, battle = result.battle, message = "대전 상대를 찾았어요!") }
                        startBattlePolling()
                    }
                }
            }
        }
    }

    private fun startBattlePolling() {
        battleJob?.cancel()
        val token = tokenVault.load() ?: return
        battleJob = viewModelScope.launch {
            while (isActive) {
                val current = mutableState.value.battle ?: break
                if (current.status == "FINISHED") break
                delay(500)
                runCatching { api.getBattle(token, current.id) }
                    .onSuccess { updated ->
                        mutableState.update { it.copy(battle = updated, battleConnectionLost = false) }
                        UnityBattleMessenger.send("SetTransportState", "CONNECTED")
                        syncUnityBattle(updated)
                    }
                    .onFailure {
                        mutableState.update { it.copy(battleConnectionLost = true) }
                        UnityBattleMessenger.send("SetTransportState", "DISCONNECTED")
                    }
            }
        }
    }

    private fun syncUnityBattle(battle: BattleState) {
        if (lastUnityBattleId != battle.id) {
            lastUnityBattleId = battle.id
            lastUnityRoundSent = 0
        }
        val last = battle.history.lastOrNull()
        if (last != null && last.round > lastUnityRoundSent) {
            UnityBattleMessenger.send("ApplyRoundJson", ArBattlePayloadFactory.round(last, battle).toString())
            lastUnityRoundSent = last.round
        }
        UnityBattleMessenger.send("ApplySnapshotJson", ArBattlePayloadFactory.snapshot(battle).toString())
    }

    private fun refreshProfile() {
        val token = tokenVault.load() ?: return
        viewModelScope.launch {
            runCatching { api.getMe(token) }
                .onSuccess { profile -> mutableState.update { it.copy(profile = profile) } }
        }
    }

    private fun launchBusy(block: suspend () -> Unit) {
        viewModelScope.launch {
            mutableState.update { it.copy(loading = true, message = null) }
            runCatching { block() }
                .onFailure { error -> mutableState.update { it.copy(message = error.message ?: "처리 중 오류가 발생했습니다.") } }
            mutableState.update { it.copy(loading = false) }
        }
    }

    override fun onCleared() {
        matchmakingJob?.cancel()
        battleJob?.cancel()
        super.onCleared()
    }
}

class MainViewModelFactory(
    private val api: BigiApiClient,
    private val tokenVault: TokenVault,
    private val health: HealthConnectStepReader,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        return MainViewModel(api, tokenVault, health) as T
    }
}
