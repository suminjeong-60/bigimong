using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleNativeBridge : MonoBehaviour
    {
        public const string GameObjectName = "BigimongARBridge";
        private const string IntentExtra = "com.bigimong.app.AR_BATTLE_PAYLOAD";
        [SerializeField] private ArBattleDirector director;
        [SerializeField] private ArBattleHud hud;
        private int hudRound;

        private void Awake()
        {
            gameObject.name = GameObjectName;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = activity.Call<AndroidJavaObject>("getIntent");
            var payload = intent.Call<string>("getStringExtra", IntentExtra);
            if (!string.IsNullOrWhiteSpace(payload)) StartBattleJson(payload);
#endif
        }

        public void StartBattleJson(string json)
        {
            var message = JsonUtility.FromJson<BattleStartMessage>(json);
            if (message == null || message.schemaVersion != 1)
            {
                SendNative("AR_ERROR", "{\"reason\":\"unsupported_payload\"}");
                return;
            }
            director?.StartBattle(message);
            hudRound = message.round;
            hud?.BeginTurn(message.deadlineAt, message.serverNow);
            SendNative("AR_READY", $"{{\"battleId\":\"{message.battleId}\"}}");
        }

        public void ApplySnapshotJson(string json)
        {
            var snapshot = JsonUtility.FromJson<BattleSnapshotMessage>(json);
            director?.ApplySnapshot(snapshot);
            if (snapshot != null && snapshot.status == "ACTIVE" && snapshot.round != hudRound)
            {
                hudRound = snapshot.round;
                hud?.BeginTurn(snapshot.deadlineAt, snapshot.serverNow);
            }
        }

        public void ApplyRoundJson(string json) =>
            director?.ApplyRound(JsonUtility.FromJson<BattleRoundMessage>(json));

        public void SetTransportState(string state) =>
            director?.SetDisconnected(state != "CONNECTED");

        public void RequestReanchor() => director?.RequestReanchor();

        public void SubmitChoice(string direction)
        {
            if (direction != "LEFT" && direction != "CENTER" && direction != "RIGHT") return;
            SendNative("CHOICE", $"{{\"direction\":\"{direction}\"}}");
        }

        public void PublishCloudAnchorId(string cloudAnchorId) =>
            SendNative("CLOUD_ANCHOR_HOSTED", $"{{\"cloudAnchorId\":\"{cloudAnchorId}\"}}");

        public void ReportAnchorError(string reason) =>
            SendNative("AR_ANCHOR_ERROR", $"{{\"reason\":\"{reason}\"}}");

        private static void SendNative(string type, string payload)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var callback = new AndroidJavaClass("com.bigidragon.app.ar.ArBattleNativeCallback");
            callback.CallStatic("onUnityEvent", type, payload);
#else
            Debug.Log($"Bigimong native event {type}: {payload}");
#endif
        }
    }
}
