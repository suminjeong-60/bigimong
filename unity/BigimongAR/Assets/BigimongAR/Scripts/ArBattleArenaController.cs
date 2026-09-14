using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Bigimong.AR
{
    public sealed class ArBattleArenaController : MonoBehaviour
    {
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private GameObject placementIndicator;
        [SerializeField] private GameObject battleRingPrefab;

        private readonly List<ARRaycastHit> hits = new();
        private Pose candidatePose;
        private bool hasCandidate;
        private GameObject arenaRoot;
        private GameObject ringVisual;
        private float requestedRingDiameter = 1.6f;
        private bool placementEnabled = true;

        public event Action<Transform> ArenaPlaced;
        public bool IsPlaced => arenaRoot != null;
        public Transform ArenaTransform => arenaRoot != null ? arenaRoot.transform : null;
        public bool IsScreenFixed { get; private set; }
        public void SetPlacementEnabled(bool enabled)
        {
            placementEnabled = enabled;
            if (!enabled) placementIndicator?.SetActive(false);
        }

        private void Update()
        {
            if (IsPlaced || !placementEnabled) return;
            UpdateCandidate(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Input.touchCount == 0) return;
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touch.fingerId)))
                TryPlace(touch.position);
        }

        public bool TryPlace(Vector2 screenPosition)
        {
            if (!placementEnabled) return false;
            UpdateCandidate(screenPosition);
            if (!hasCandidate) return false;
            return PlaceAtPose(candidatePose);
        }

        public bool PlaceAtPose(Pose pose)
        {
            if (!placementEnabled) return false;
            if (arenaRoot != null) Destroy(arenaRoot);
            IsScreenFixed = false;
            arenaRoot = new GameObject("Bigimong AR Arena");
            arenaRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            ringVisual = battleRingPrefab != null
                ? Instantiate(battleRingPrefab, arenaRoot.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringVisual.name = "Battle Ring Visual";
            ringVisual.transform.SetParent(arenaRoot.transform, false);
            ApplyRingSize();
            placementIndicator?.SetActive(false);
            SetPlanesVisible(false);
            ArenaPlaced?.Invoke(arenaRoot.transform);
            return true;
        }

        public bool PlaceScreenFixed(Transform cameraTransform)
        {
            if (!placementEnabled || cameraTransform == null) return false;
            if (arenaRoot != null) Destroy(arenaRoot);
            arenaRoot = new GameObject("Bigimong Screen Fixed Arena");
            arenaRoot.transform.SetParent(cameraTransform, false);
            arenaRoot.transform.localPosition = new Vector3(0f, -0.8f, 1.8f);
            arenaRoot.transform.localRotation = Quaternion.identity;
            IsScreenFixed = true;
            ringVisual = battleRingPrefab != null
                ? Instantiate(battleRingPrefab, arenaRoot.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringVisual.name = "Battle Ring Visual";
            ringVisual.transform.SetParent(arenaRoot.transform, false);
            ApplyRingSize();
            placementIndicator?.SetActive(false);
            SetPlanesVisible(false);
            ArenaPlaced?.Invoke(arenaRoot.transform);
            return true;
        }

        public void ConfigureBattleSize(float tallestActorMeters)
        {
            requestedRingDiameter = Mathf.Clamp(tallestActorMeters * 2.25f, 1.6f, 5f);
            ApplyRingSize();
        }

        public void RequestReanchor()
        {
            if (arenaRoot != null) Destroy(arenaRoot);
            arenaRoot = null;
            ringVisual = null;
            IsScreenFixed = false;
            hasCandidate = false;
            placementIndicator?.SetActive(placementEnabled);
            SetPlanesVisible(true);
        }

        private void ApplyRingSize()
        {
            if (ringVisual == null) return;
            ringVisual.transform.localScale = new Vector3(requestedRingDiameter, 1f, requestedRingDiameter);
        }

        private void UpdateCandidate(Vector2 screenPosition)
        {
            hasCandidate = raycastManager != null &&
                raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon);
            if (!hasCandidate)
            {
                placementIndicator?.SetActive(false);
                return;
            }
            candidatePose = hits[0].pose;
            if (placementIndicator != null)
            {
                placementIndicator.SetActive(true);
                placementIndicator.transform.SetPositionAndRotation(candidatePose.position, candidatePose.rotation);
            }
        }

        private void SetPlanesVisible(bool visible)
        {
            if (planeManager == null) return;
            planeManager.enabled = visible;
            foreach (var plane in planeManager.trackables) plane.gameObject.SetActive(visible);
        }
    }
}
