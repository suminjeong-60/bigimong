package com.bigidragon.app.ar

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import com.bigidragon.app.data.BattlePlayer
import com.bigidragon.app.data.BattleRound
import com.bigidragon.app.data.BattleState
import org.json.JSONObject

const val BIGIMONG_AR_PAYLOAD = "com.bigimong.app.AR_BATTLE_PAYLOAD"

data class ArLaunchResult(val launched: Boolean, val message: String)

object ArBattleLauncher {
    private val unityActivities = listOf(
        "com.unity3d.player.UnityPlayerGameActivity",
        "com.unity3d.player.UnityPlayerActivity",
    )

    fun launch(context: Context, battle: BattleState): ArLaunchResult {
        if (!context.packageManager.hasSystemFeature(PackageManager.FEATURE_CAMERA_AR)) {
            return ArLaunchResult(false, "이 기기는 AR을 지원하지 않아 일반 대전 화면을 사용합니다.")
        }
        val unityActivity = unityActivities.firstNotNullOfOrNull { name ->
            runCatching { Class.forName(name) }.getOrNull()
        } ?: return ArLaunchResult(false, "Unity AR 모듈을 Android 앱에 내보낸 뒤 사용할 수 있습니다.")

        val intent = Intent(context, unityActivity).apply {
            putExtra(BIGIMONG_AR_PAYLOAD, ArBattlePayloadFactory.start(battle).toString())
            addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP)
        }
        context.startActivity(intent)
        return ArLaunchResult(true, "AR 전투를 시작합니다.")
    }
}

object ArBattlePayloadFactory {
    fun start(battle: BattleState): JSONObject = JSONObject().apply {
        put("schemaVersion", 1)
        put("battleId", battle.id)
        put("mode", battle.mode)
        put("stake", battle.stake)
        put("round", battle.round)
        put("serverNow", battle.serverNow)
        put("deadlineAt", battle.deadlineAt)
        put("youAre", battle.youAre)
        put("transport", if (battle.mode == "NEARBY") "BLE_SERVER_AUTH" else "SERVER")
        put("cloudAnchorId", battle.cloudAnchorId ?: "")
        put("playerA", player(battle.playerA))
        put("playerB", player(battle.playerB))
    }

    private fun player(player: BattlePlayer): JSONObject = JSONObject().apply {
        put("userId", player.userId)
        put("hp", player.hp)
        put("name", player.dragon.name)
        put("artId", player.dragon.artId)
        put("stage", player.dragon.stage)
        put("attack", player.dragon.attack)
        put("evasion", player.dragon.evasion)
        put("vitality", player.dragon.vitality)
    }

    fun snapshot(battle: BattleState): JSONObject = JSONObject().apply {
        put("battleId", battle.id)
        put("status", battle.status)
        put("round", battle.round)
        put("attacker", battle.attacker)
        put("serverNow", battle.serverNow)
        put("deadlineAt", battle.deadlineAt)
        put("hpA", battle.playerA.hp)
        put("hpB", battle.playerB.hp)
        put("winner", battle.winner ?: "")
        put("cloudAnchorId", battle.cloudAnchorId ?: "")
    }

    fun round(round: BattleRound, battle: BattleState): JSONObject = JSONObject().apply {
        put("round", round.round)
        put("attacker", round.attacker)
        put("defender", round.defender)
        put("attackDirection", round.attackDirection)
        put("defendDirection", round.defendDirection)
        put("attackAutomatic", round.attackAutomatic)
        put("defendAutomatic", round.defendAutomatic)
        put("outcome", round.outcome)
        put("damage", round.damage)
        put("hpA", battle.playerA.hp)
        put("hpB", battle.playerB.hp)
    }
}

object UnityBattleMessenger {
    private const val target = "BigimongARBridge"

    fun send(method: String, payload: String): Boolean = runCatching {
        val unityPlayer = Class.forName("com.unity3d.player.UnityPlayer")
        val unitySendMessage = unityPlayer.getMethod(
            "UnitySendMessage",
            String::class.java,
            String::class.java,
            String::class.java,
        )
        unitySendMessage.invoke(null, target, method, payload)
    }.isSuccess
}

object ArBattleNativeCallback {
    @Volatile private var listener: ((String, String) -> Unit)? = null

    fun setListener(value: ((String, String) -> Unit)?) {
        listener = value
    }

    @JvmStatic
    fun onUnityEvent(type: String, payload: String) {
        listener?.invoke(type, payload)
    }
}
