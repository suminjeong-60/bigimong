package com.bigidragon.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

class PermissionsRationaleActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                Column(
                    Modifier.fillMaxSize().padding(24.dp),
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text("비기몽 걸음 수 이용 안내", style = MaterialTheme.typography.headlineSmall)
                    Text(
                        "비기몽은 알 부화와 비기 보상을 위해 오늘의 누적 걸음 수만 읽습니다. " +
                            "심박수나 다른 건강 정보는 요청하지 않으며, 언제든 Health Connect 설정에서 권한을 취소할 수 있습니다.",
                        modifier = Modifier.padding(top = 16.dp),
                    )
                }
            }
        }
    }
}
