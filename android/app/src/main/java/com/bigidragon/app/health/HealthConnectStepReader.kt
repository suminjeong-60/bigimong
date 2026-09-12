package com.bigidragon.app.health

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.PermissionController
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.time.TimeRangeFilter
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime

enum class HealthAvailability {
    AVAILABLE,
    UPDATE_REQUIRED,
    UNAVAILABLE,
}

class HealthConnectStepReader(private val context: Context) {
    val permissions: Set<String> = setOf(HealthPermission.getReadPermission(StepsRecord::class))

    fun availability(): HealthAvailability = when (HealthConnectClient.getSdkStatus(context)) {
        HealthConnectClient.SDK_AVAILABLE -> HealthAvailability.AVAILABLE
        HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> HealthAvailability.UPDATE_REQUIRED
        else -> HealthAvailability.UNAVAILABLE
    }

    suspend fun hasPermission(): Boolean {
        if (availability() != HealthAvailability.AVAILABLE) return false
        return client().permissionController.getGrantedPermissions().containsAll(permissions)
    }

    suspend fun readTodaySteps(): Long {
        check(availability() == HealthAvailability.AVAILABLE) { "Health Connect를 사용할 수 없습니다." }
        check(hasPermission()) { "걸음 수 읽기 권한이 필요합니다." }
        val zone = ZoneId.of("Asia/Seoul")
        val start = ZonedDateTime.now(zone).toLocalDate().atStartOfDay(zone).toInstant()
        val response = client().aggregate(
            AggregateRequest(
                metrics = setOf(StepsRecord.COUNT_TOTAL),
                timeRangeFilter = TimeRangeFilter.between(start, Instant.now()),
            ),
        )
        return response[StepsRecord.COUNT_TOTAL] ?: 0L
    }

    fun todayKst(): String = ZonedDateTime.now(ZoneId.of("Asia/Seoul")).toLocalDate().toString()

    fun permissionContract() = PermissionController.createRequestPermissionResultContract()

    private fun client(): HealthConnectClient = HealthConnectClient.getOrCreate(context)
}
