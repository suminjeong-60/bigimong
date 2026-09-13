using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleNativeBridge : MonoBehaviour
    {
        public const string GameObjectName = "BigimongARBridge";
        private const string IntentExtra = "com.bigimong.app.AR_BATTLE_PAYLOAD";
        [SerializeField] private ArBattleDirector director;
        [SerializeField] private ArBattleHud hud;
        [SerializeField] private ArBattleOfflineDemo offlineDemo;
        [SerializeField] private OfflineBetaFlowController offlineFlow;
        private int hudRound;
        private bool nativeTransportAvailable;

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
            nativeTransportAvailable = !string.IsNullOrWhiteSpace(payload);
            if (nativeTransportAvailable) StartBattleJson(payload);
            else offlineFlow?.StartBeta();
#else
            offlineFlow?.StartBeta();
#endif
        }

        public void StartBattleJson(string json)
        {
            offlineFlow?.DisableForOnlineHost();
            var message = JsonUtility.FromJson<BattleStartMessage>(json);
            StartBattleMessage(message, true);
        }

        public void StartBattleMessage(BattleStartMessage message, bool notifyNative)
        {
            if (message == null || message.schemaVersion != 1)
            {
                SendNative("AR_ERROR", "{\"reason\":\"unsupported_payload\"}");
                return;
            }
            director?.StartBattle(message);
            hudRound = message.round;
            hud?.BeginTurn(message.deadlineAt, message.serverNow);
            if (notifyNative) SendNative("AR_READY", $"{{\"battleId\":\"{message.battleId}\"}}");
        }

        public void ApplySnapshotJson(string json)
        {
            var snapshot = JsonUtility.FromJson<BattleSnapshotMessage>(json);
            ApplySnapshotMessage(snapshot);
        }

        public void ApplySnapshotMessage(BattleSnapshotMessage snapshot)
        {
            director?.ApplySnapshot(snapshot);
            if (snapshot != null && snapshot.status == "ACTIVE" && snapshot.round != hudRound)
            {
                hudRound = snapshot.round;
                hud?.BeginTurn(snapshot.deadlineAt, snapshot.serverNow);
            }
        }

        public void ApplyRoundJson(string json) =>
            ApplyRoundMessage(JsonUtility.FromJson<BattleRoundMessage>(json));

        public void ApplyRoundMessage(BattleRoundMessage message) => director?.ApplyRound(message);

        public void SetTransportState(string state) =>
            director?.SetDisconnected(state != "CONNECTED");

        public void RequestReanchor() => director?.RequestReanchor();

        public void SubmitChoice(string direction)
        {
            if (direction != "LEFT" && direction != "CENTER" && direction != "RIGHT") return;
            if (offlineDemo != null && offlineDemo.IsActive)
            {
                if (offlineFlow == null || !offlineFlow.CanSubmitChoice) return;
                offlineDemo.SubmitChoice(direction);
                return;
            }
            SendNative("CHOICE", $"{{\"direction\":\"{direction}\"}}");
        }

        public void RestartOfflineDemo() => offlineFlow?.Rematch();

        public void PublishCloudAnchorId(string cloudAnchorId) =>
            SendNative("CLOUD_ANCHOR_HOSTED", $"{{\"cloudAnchorId\":\"{cloudAnchorId}\"}}");

        public void ReportAnchorError(string reason) =>
            SendNative("AR_ANCHOR_ERROR", $"{{\"reason\":\"{reason}\"}}");

        private void SendNative(string type, string payload)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!nativeTransportAvailable)
            {
                Debug.Log($"Bigimong offline event {type}: {payload}");
                return;
            }
            try
            {
                using var callback = new AndroidJavaClass("com.bigidragon.app.ar.ArBattleNativeCallback");
                callback.CallStatic("onUnityEvent", type, payload);
            }
            catch (AndroidJavaException exception)
            {
                Debug.LogError($"Bigimong native callback failed: {exception.Message}");
            }
#else
            Debug.Log($"Bigimong native event {type}: {payload}");
#endif
        }
    }
}
