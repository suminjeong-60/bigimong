package com.bigidragon.app.ar

import android.annotation.SuppressLint
import android.bluetooth.BluetoothManager
import android.bluetooth.le.AdvertiseCallback
import android.bluetooth.le.AdvertiseData
import android.bluetooth.le.AdvertiseSettings
import android.bluetooth.le.ScanCallback
import android.bluetooth.le.ScanFilter
import android.bluetooth.le.ScanResult
import android.bluetooth.le.ScanSettings
import android.content.Context
import android.os.ParcelUuid
import java.nio.charset.StandardCharsets
import java.security.SecureRandom
import java.util.UUID

enum class BleDiscoveryState { IDLE, ADVERTISING, SCANNING, FOUND, ERROR }

/** BLE is used only to find the intended nearby opponent and exchange a pairing code. */
class BleNearbyDiscovery(context: Context) {
    companion object {
        val SERVICE_UUID: UUID = UUID.fromString("eac18d5d-f3fd-4a99-93ad-6d410fcb6018")
        private val alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".toCharArray()

        fun createPairingCode(length: Int = 8): String {
            require(length in 6..12)
            val random = SecureRandom()
            return buildString(length) {
                repeat(length) { append(alphabet[random.nextInt(alphabet.size)]) }
            }
        }
    }

    private val adapter = context.getSystemService(BluetoothManager::class.java)?.adapter
    private val service = ParcelUuid(SERVICE_UUID)
    private var advertiserCallback: AdvertiseCallback? = null
    private var scannerCallback: ScanCallback? = null

    @SuppressLint("MissingPermission")
    fun advertise(pairingCode: String, onState: (BleDiscoveryState) -> Unit) {
        stop()
        val advertiser = adapter?.bluetoothLeAdvertiser ?: run {
            onState(BleDiscoveryState.ERROR)
            return
        }
        val callback = object : AdvertiseCallback() {
            override fun onStartSuccess(settingsInEffect: AdvertiseSettings?) = onState(BleDiscoveryState.ADVERTISING)
            override fun onStartFailure(errorCode: Int) = onState(BleDiscoveryState.ERROR)
        }
        advertiserCallback = callback
        advertiser.startAdvertising(
            AdvertiseSettings.Builder()
                .setAdvertiseMode(AdvertiseSettings.ADVERTISE_MODE_LOW_LATENCY)
                .setConnectable(false)
                .setTimeout(0)
                .build(),
            AdvertiseData.Builder()
                .addServiceUuid(service)
                .addServiceData(service, pairingCode.toByteArray(StandardCharsets.US_ASCII))
                .setIncludeDeviceName(false)
                .build(),
            callback,
        )
    }

    @SuppressLint("MissingPermission")
    fun scan(onPairingCode: (String) -> Unit, onState: (BleDiscoveryState) -> Unit) {
        stop()
        val scanner = adapter?.bluetoothLeScanner ?: run {
            onState(BleDiscoveryState.ERROR)
            return
        }
        val callback = object : ScanCallback() {
            override fun onScanResult(callbackType: Int, result: ScanResult) {
                val code = result.scanRecord?.getServiceData(service)
                    ?.let { bytes -> String(bytes, StandardCharsets.US_ASCII) }
                    ?.trim()
                    ?.uppercase()
                    .orEmpty()
                if (code.matches(Regex("^[A-Z0-9-]{6,32}$"))) {
                    onState(BleDiscoveryState.FOUND)
                    onPairingCode(code)
                }
            }

            override fun onScanFailed(errorCode: Int) = onState(BleDiscoveryState.ERROR)
        }
        scannerCallback = callback
        scanner.startScan(
            listOf(ScanFilter.Builder().setServiceUuid(service).build()),
            ScanSettings.Builder().setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY).build(),
            callback,
        )
        onState(BleDiscoveryState.SCANNING)
    }

    @SuppressLint("MissingPermission")
    fun stop() {
        advertiserCallback?.let { adapter?.bluetoothLeAdvertiser?.stopAdvertising(it) }
        scannerCallback?.let { adapter?.bluetoothLeScanner?.stopScan(it) }
        advertiserCallback = null
        scannerCallback = null
    }
}
