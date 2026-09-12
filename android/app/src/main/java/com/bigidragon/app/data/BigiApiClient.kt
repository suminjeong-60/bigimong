package com.bigidragon.app.data

import com.bigidragon.app.BuildConfig
import java.net.HttpURLConnection
import java.net.URL
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject

class BigiApiClient(private val baseUrl: String = BuildConfig.API_BASE_URL) {
    suspend fun createGuest(dragonName: String, ownerPetName: String): GuestAccount =
        request("POST", "/api/v1/accounts/guest", body = JSONObject().apply {
            put("dragonName", dragonName)
            put("ownerPetName", ownerPetName)
            put("species", "tyrannosaurus")
        }).let { response ->
            GuestAccount(response.getString("token"), parseUser(response.getJSONObject("user")))
        }

    suspend fun getMe(token: String): UserProfile =
        parseUser(request("GET", "/api/v1/me", token = token))

    suspend fun openStarterGift(token: String): UserProfile =
        parseUser(request("POST", "/api/v1/journey/gift/open", token = token, body = JSONObject()))

    suspend fun cleanEgg(token: String): UserProfile =
        parseUser(request("POST", "/api/v1/journey/egg/clean", token = token, body = JSONObject()))

    suspend fun hatchEgg(token: String): UserProfile =
        parseUser(request("POST", "/api/v1/journey/egg/hatch", token = token, body = JSONObject()))

    suspend fun syncSteps(token: String, day: String, totalSteps: Long): StepSyncResult {
        val key = "hc:$day:$totalSteps"
        val response = request(
            method = "POST",
            path = "/api/v1/steps/sync",
            token = token,
            headers = mapOf("Idempotency-Key" to key),
            body = JSONObject().apply {
                put("day", day)
                put("totalSteps", totalSteps)
            },
        )
        return StepSyncResult(
            rewarded = response.getInt("rewarded"),
            eggProgressAdded = response.optInt("eggProgressAdded", 0),
            balance = response.getInt("balance"),
            duplicate = response.optBoolean("duplicate", false),
        )
    }

    suspend fun joinRemoteBattle(token: String, stake: Int): MatchmakingResult {
        val response = request(
            method = "POST",
            path = "/api/v1/matchmaking",
            token = token,
            body = JSONObject().apply {
                put("mode", "REMOTE")
                put("stake", stake)
            },
        )
        return parseMatchmaking(response)
    }

    suspend fun joinNearbyBattle(token: String, pairingCode: String): MatchmakingResult {
        val response = request(
            method = "POST",
            path = "/api/v1/matchmaking",
            token = token,
            body = JSONObject().apply {
                put("mode", "NEARBY")
                put("stake", 1_000)
                put("pairingCode", pairingCode)
            },
        )
        return parseMatchmaking(response)
    }

    suspend fun getMatchmaking(token: String): MatchmakingResult =
        parseMatchmaking(request("GET", "/api/v1/matchmaking", token = token))

    suspend fun cancelMatchmaking(token: String) {
        request("DELETE", "/api/v1/matchmaking", token = token)
    }

    suspend fun getBattle(token: String, battleId: String): BattleState =
        parseBattle(request("GET", "/api/v1/battles/$battleId", token = token))

    suspend fun submitBattleChoice(token: String, battleId: String, direction: String): BattleState {
        val response = request(
            method = "POST",
            path = "/api/v1/battles/$battleId/choice",
            token = token,
            body = JSONObject().apply { put("direction", direction) },
        )
        return parseBattle(response.getJSONObject("battle"))
    }

    suspend fun publishCloudAnchor(token: String, battleId: String, cloudAnchorId: String) {
        request(
            method = "POST",
            path = "/api/v1/battles/$battleId/anchor",
            token = token,
            body = JSONObject().apply { put("cloudAnchorId", cloudAnchorId) },
        )
    }

    private suspend fun request(
        method: String,
        path: String,
        token: String? = null,
        headers: Map<String, String> = emptyMap(),
        body: JSONObject? = null,
    ): JSONObject = withContext(Dispatchers.IO) {
        val connection = URL("$baseUrl$path").openConnection() as HttpURLConnection
        try {
            connection.requestMethod = method
            connection.connectTimeout = 8_000
            connection.readTimeout = 12_000
            connection.setRequestProperty("Accept", "application/json")
            if (token != null) connection.setRequestProperty("Authorization", "Bearer $token")
            headers.forEach(connection::setRequestProperty)
            if (body != null) {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                connection.outputStream.bufferedWriter(Charsets.UTF_8).use { it.write(body.toString()) }
            }
            val status = connection.responseCode
            val stream = if (status in 200..299) connection.inputStream else connection.errorStream
            val text = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            val json = if (text.isBlank()) JSONObject() else JSONObject(text)
            if (status !in 200..299) {
                throw BigiApiException(status, json.optString("message", "서버 요청에 실패했습니다."))
            }
            json
        } finally {
            connection.disconnect()
        }
    }

    private fun parseUser(json: JSONObject): UserProfile {
        val journeyJson = json.getJSONObject("journey")
        val eggJson = journeyJson.optJSONObject("egg")
        val dragonJson = json.optJSONObject("dragon")
        return UserProfile(
            id = json.getString("id"),
            wallet = json.getInt("wallet"),
            journey = JourneyState(
                phase = journeyJson.getString("phase"),
                giftOpened = journeyJson.getBoolean("giftOpened"),
                egg = eggJson?.let {
                    EggState(
                        id = it.getString("id"),
                        progress = it.getInt("progress"),
                        required = it.getInt("required"),
                        lastCleanedAt = it.optNullableLong("lastCleanedAt"),
                        nextCleanAt = it.optNullableLong("nextCleanAt"),
                    )
                },
            ),
            dragon = dragonJson?.let { dragon ->
                val stats = dragon.getJSONObject("stats")
                DragonProfile(
                    id = dragon.getString("id"),
                    artId = dragon.optInt("artId", 1),
                    name = dragon.getString("name"),
                    ownerPetName = dragon.optString("ownerPetName"),
                    species = dragon.getString("species"),
                    level = dragon.getInt("level"),
                    stage = dragon.getString("stage"),
                    attack = stats.getInt("attack"),
                    evasion = stats.getInt("evasion"),
                    vitality = stats.getInt("vitality"),
                    speciesName = dragon.optString("speciesName", dragon.getString("species")),
                    skillName = dragon.optString("skillName"),
                    emoji = dragon.optString("emoji", "🦖"),
                    eyeColorName = dragon.optJSONObject("eyeColor")?.optString("name").orEmpty(),
                    wingColorName = dragon.optJSONObject("wingColor")?.optString("name").orEmpty(),
                )
            },
        )
    }

    private fun parseMatchmaking(json: JSONObject): MatchmakingResult {
        val battleJson = json.optJSONObject("battle")
        return MatchmakingResult(
            status = json.getString("status"),
            battleId = json.optString("battleId").ifBlank { battleJson?.optString("id").orEmpty() }.ifBlank { null },
            mode = json.optString("mode").ifBlank { battleJson?.optString("mode").orEmpty() }.ifBlank { null },
            stake = if (json.has("stake")) json.optInt("stake", 0) else battleJson?.optInt("stake", 0) ?: 0,
            battle = battleJson?.let(::parseBattle),
        )
    }

    private fun parseBattle(json: JSONObject): BattleState {
        val players = json.getJSONObject("players")
        val historyJson = json.getJSONArray("history")
        return BattleState(
            id = json.getString("id"),
            mode = json.getString("mode"),
            stake = json.getInt("stake"),
            status = json.getString("status"),
            round = json.getInt("round"),
            attacker = json.getString("attacker"),
            playerA = parseBattlePlayer(players.getJSONObject("A")),
            playerB = parseBattlePlayer(players.getJSONObject("B")),
            history = buildList {
                for (index in 0 until historyJson.length()) add(parseBattleRound(historyJson.getJSONObject(index)))
            },
            winner = json.optString("winner").ifBlank { null },
            burned = json.optInt("burned", 0),
            payout = json.optInt("payout", 0),
            youAre = json.getString("youAre"),
            serverNow = json.optLong("serverNow", System.currentTimeMillis()),
            deadlineAt = json.getLong("deadlineAt"),
            cloudAnchorId = json.optString("cloudAnchorId").ifBlank { null },
            choiceSubmitted = json.optBoolean("choiceSubmitted", false),
            opponentChoiceSubmitted = json.optBoolean("opponentChoiceSubmitted", false),
        )
    }

    private fun parseBattlePlayer(json: JSONObject): BattlePlayer {
        val dragon = json.getJSONObject("dragon")
        val stats = dragon.getJSONObject("stats")
        return BattlePlayer(
            userId = json.getString("userId"),
            wallet = json.getInt("wallet"),
            hp = json.getInt("hp"),
            dragon = BattleDragon(
                artId = dragon.optInt("artId", 1),
                name = dragon.getString("name"),
                species = dragon.getString("species"),
                level = dragon.getInt("level"),
                stage = dragon.getString("stage"),
                attack = stats.getInt("attack"),
                evasion = stats.getInt("evasion"),
                vitality = stats.getInt("vitality"),
            ),
        )
    }

    private fun parseBattleRound(json: JSONObject): BattleRound {
        val motion = json.getJSONObject("motion")
        return BattleRound(
            round = json.getInt("round"),
            attacker = json.getString("attacker"),
            defender = json.getString("defender"),
            attackDirection = json.getString("attackDirection"),
            defendDirection = json.getString("defendDirection"),
            attackAutomatic = json.getBoolean("attackAutomatic"),
            defendAutomatic = json.getBoolean("defendAutomatic"),
            outcome = json.getString("outcome"),
            damage = json.getInt("damage"),
            motion = BattleMotion(
                attacker = motion.getString("attacker"),
                defender = motion.getString("defender"),
            ),
        )
    }

    private fun JSONObject.optNullableLong(name: String): Long? =
        if (isNull(name) || !has(name)) null else getLong(name)
}

class BigiApiException(val status: Int, override val message: String) : Exception(message)
