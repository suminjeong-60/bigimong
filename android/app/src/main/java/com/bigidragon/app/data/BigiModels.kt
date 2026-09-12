package com.bigidragon.app.data

data class DragonProfile(
    val id: String,
    val artId: Int,
    val name: String,
    val ownerPetName: String,
    val species: String,
    val level: Int,
    val stage: String,
    val attack: Int,
    val evasion: Int,
    val vitality: Int,
    val speciesName: String,
    val skillName: String,
    val emoji: String,
    val eyeColorName: String,
    val wingColorName: String,
)

data class EggState(
    val id: String,
    val progress: Int,
    val required: Int,
    val lastCleanedAt: Long?,
    val nextCleanAt: Long?,
)

data class JourneyState(
    val phase: String,
    val giftOpened: Boolean,
    val egg: EggState?,
)

data class UserProfile(
    val id: String,
    val wallet: Int,
    val journey: JourneyState,
    val dragon: DragonProfile?,
)

data class GuestAccount(
    val token: String,
    val user: UserProfile,
)

data class StepSyncResult(
    val rewarded: Int,
    val eggProgressAdded: Int,
    val balance: Int,
    val duplicate: Boolean,
)

data class MatchmakingResult(
    val status: String,
    val battleId: String?,
    val mode: String? = null,
    val stake: Int = 0,
    val battle: BattleState? = null,
)

data class BattleDragon(
    val artId: Int,
    val name: String,
    val species: String,
    val level: Int,
    val stage: String,
    val attack: Int,
    val evasion: Int,
    val vitality: Int,
)

data class BattlePlayer(
    val userId: String,
    val wallet: Int,
    val hp: Int,
    val dragon: BattleDragon,
)

data class BattleMotion(
    val attacker: String,
    val defender: String,
)

data class BattleRound(
    val round: Int,
    val attacker: String,
    val defender: String,
    val attackDirection: String,
    val defendDirection: String,
    val attackAutomatic: Boolean,
    val defendAutomatic: Boolean,
    val outcome: String,
    val damage: Int,
    val motion: BattleMotion,
)

data class BattleState(
    val id: String,
    val mode: String,
    val stake: Int,
    val status: String,
    val round: Int,
    val attacker: String,
    val playerA: BattlePlayer,
    val playerB: BattlePlayer,
    val history: List<BattleRound>,
    val winner: String?,
    val burned: Int,
    val payout: Int,
    val youAre: String,
    val serverNow: Long,
    val deadlineAt: Long,
    val cloudAnchorId: String?,
    val choiceSubmitted: Boolean,
    val opponentChoiceSubmitted: Boolean,
)
