package com.bigidragon.app.ar

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.ContextCompat

object ArBattlePermissionPolicy {
    fun requiredPermissions(includeNearby: Boolean): Array<String> = buildList {
        add(Manifest.permission.CAMERA)
        if (includeNearby) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                add(Manifest.permission.BLUETOOTH_SCAN)
                add(Manifest.permission.BLUETOOTH_CONNECT)
                add(Manifest.permission.BLUETOOTH_ADVERTISE)
            } else {
                add(Manifest.permission.ACCESS_FINE_LOCATION)
            }
        }
    }.toTypedArray()

    fun isGranted(context: Context, includeNearby: Boolean): Boolean =
        requiredPermissions(includeNearby).all { permission ->
            ContextCompat.checkSelfPermission(context, permission) == PackageManager.PERMISSION_GRANTED
        }
}
