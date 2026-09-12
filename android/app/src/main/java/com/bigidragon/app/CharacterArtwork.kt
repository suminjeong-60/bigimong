package com.bigidragon.app

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.sp

/** Selects one of the 90 packaged character renders by catalog number and growth stage. */
@Composable
fun CharacterArtwork(
    artId: Int,
    stage: String,
    fallbackEmoji: String,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val stageKey = when (stage) {
        "YOUTH" -> "youth"
        "ADULT" -> "adult"
        else -> "growth"
    }
    val resourceName = remember(artId, stageKey) {
        "bigimong_${artId.coerceIn(1, 30).toString().padStart(2, '0')}_$stageKey"
    }
    val resourceId = remember(resourceName) {
        context.resources.getIdentifier(resourceName, "drawable", context.packageName)
    }

    if (resourceId != 0) {
        Image(
            painter = painterResource(resourceId),
            contentDescription = "비기몽 캐릭터 ${artId.coerceIn(1, 30)}번 $stageKey",
            modifier = modifier,
            contentScale = ContentScale.Fit,
        )
    } else {
        Box(modifier = modifier, contentAlignment = Alignment.Center) {
            Text(fallbackEmoji, fontSize = 64.sp)
        }
    }
}
