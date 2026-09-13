using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Bigimong.AR
{
    public sealed class AvatarCreatorController : MonoBehaviour
    {
        private static readonly string[] Categories = { "BODY", "FACE", "SKIN", "BROWS", "EYES", "HAIR", "HAIR_COLOR" };
        private static readonly int[] CategoryLimits = { 2, 5, 8, 6, 8, 12, 10 };

        [SerializeField] private GameObject creatorCanvas;
        [SerializeField] private GameObject battleCanvas;
        [SerializeField] private GameObject editorEntry;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform editorEntrySafeArea;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private InputField nameInput;
        [SerializeField] private Text previewNameText;
        [SerializeField] private Text categoryLabel;
        [SerializeField] private Text valueLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button masculineButton;
        [SerializeField] private Button feminineButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button editButton;
        [SerializeField] private Button[] categoryButtons;

        public event Action<AvatarProfile> Completed;
        public event Action EditingStarted;

        private AvatarProfile profile;
        private GameObject preview;
        private int categoryIndex;
        private bool listenersBound;
        private Rect lastSafeArea;
        private UnityAction[] categoryActions;
        private bool hasSavedProfile;

        public static int CategoryCount => CategoryLimits.Length;

        public static int CategoryLimit(int category)
        {
            return CategoryLimits[Mathf.Clamp(category, 0, CategoryLimits.Length - 1)];
        }

        public static int WrapSelection(int current, int delta, int limit)
        {
            return (current - 1 + delta % limit + limit) % limit + 1;
        }

        private void Awake()
        {
            var loadedProfile = AvatarProfileStore.Load();
            Initialize(loadedProfile, AvatarProfileStore.HasSavedProfile);
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea)
                ApplySafeArea();
        }

        private void OnDestroy()
        {
            if (!listenersBound)
                return;
            nameInput?.onValueChanged.RemoveListener(OnNameChanged);
            previousButton?.onClick.RemoveListener(Previous);
            nextButton?.onClick.RemoveListener(Next);
            masculineButton?.onClick.RemoveListener(SelectMasculine);
            feminineButton?.onClick.RemoveListener(SelectFeminine);
            saveButton?.onClick.RemoveListener(Save);
            editButton?.onClick.RemoveListener(OpenEditor);
            if (categoryButtons != null && categoryActions != null)
                for (var index = 0; index < Mathf.Min(categoryButtons.Length, categoryActions.Length); index++)
                    categoryButtons[index]?.onClick.RemoveListener(categoryActions[index]);
        }

        public void SelectCategory(int index)
        {
            categoryIndex = Mathf.Clamp(index, 0, Categories.Length - 1);
            RefreshLabels();
        }

        public void Previous() => ChangeSelection(-1);
        public void Next() => ChangeSelection(1);
        public void SelectMasculine() => SetBodyType("MASCULINE");
        public void SelectFeminine() => SetBodyType("FEMININE");

        public void Initialize(AvatarProfile initialProfile, bool savedProfileExists)
        {
            profile = Copy(initialProfile ?? AvatarProfile.CreateDefault());
            profile.Normalize();
            hasSavedProfile = savedProfileExists;
            categoryIndex = 0;
            if (!hasSavedProfile)
                profile.displayName = string.Empty;

            BindControls();
            ApplySafeArea();
            SetNameInput(hasSavedProfile ? profile.displayName : string.Empty);
            RefreshPreviewName();
            SetCreatorVisible(!hasSavedProfile);
            if (!hasSavedProfile)
            {
                RefreshPreview();
                RefreshLabels();
                RefreshSaveState();
            }
        }

        public void OpenEditor()
        {
            var loadedProfile = AvatarProfileStore.Load();
            if (!AvatarProfileStore.HasSavedProfile)
            {
                Initialize(loadedProfile, false);
                return;
            }
            OpenEditor(loadedProfile);
        }

        public void SetOnlineBattleMode()
        {
            creatorCanvas?.SetActive(false);
            if (previewAnchor != null) previewAnchor.gameObject.SetActive(false);
            editorEntry?.SetActive(false);
            battleCanvas?.SetActive(true);
        }

        public void OpenEditor(AvatarProfile savedProfile)
        {
            profile = Copy(savedProfile ?? AvatarProfile.CreateDefault());
            profile.Normalize();
            hasSavedProfile = true;
            categoryIndex = 0;
            SetNameInput(profile.displayName);
            RefreshPreviewName();
            RefreshPreview();
            RefreshLabels();
            RefreshSaveState();
            SetCreatorVisible(true);
            EditingStarted?.Invoke();
        }

        public void Save()
        {
            if (nameInput == null || profile == null)
                return;

            var enteredName = nameInput.text.Trim();
            profile.displayName = enteredName;
            if (enteredName.Length == 0 || !profile.IsComplete)
            {
                RefreshSaveState();
                return;
            }

            if (!AvatarProfileStore.Save(profile))
            {
                if (valueLabel != null) valueLabel.text = "저장에 실패했어요";
                return;
            }

            hasSavedProfile = true;
            SetCreatorVisible(false);
            Completed?.Invoke(profile);
        }

        private void BindControls()
        {
            if (listenersBound)
                return;
            listenersBound = true;
            nameInput?.onValueChanged.AddListener(OnNameChanged);
            previousButton?.onClick.AddListener(Previous);
            nextButton?.onClick.AddListener(Next);
            masculineButton?.onClick.AddListener(SelectMasculine);
            feminineButton?.onClick.AddListener(SelectFeminine);
            saveButton?.onClick.AddListener(Save);
            editButton?.onClick.AddListener(OpenEditor);
            if (categoryButtons == null)
                return;

            categoryActions = new UnityAction[Mathf.Min(categoryButtons.Length, Categories.Length)];
            for (var index = 0; index < categoryActions.Length; index++)
            {
                var selectedIndex = index;
                categoryActions[index] = () => SelectCategory(selectedIndex);
                categoryButtons[index]?.onClick.AddListener(categoryActions[index]);
            }
        }

        private void OnNameChanged(string value)
        {
            if (profile == null)
                return;
            profile.displayName = value;
            RefreshPreviewName();
            RefreshSaveState();
        }

        private void ChangeSelection(int delta)
        {
            var current = ValueForCategory(categoryIndex);
            var limit = CategoryLimits[categoryIndex];
            var wrapped = WrapSelection(current, delta, limit);
            SetValueForCategory(categoryIndex, wrapped);
            RefreshPreview();
            RefreshLabels();
        }

        private void SetBodyType(string bodyType)
        {
            profile.bodyType = bodyType == "FEMININE" ? "FEMININE" : "MASCULINE";
            RefreshPreview();
            RefreshLabels();
        }

        private int ValueForCategory(int index)
        {
            return index switch
            {
                0 => profile.bodyType == "FEMININE" ? 2 : 1,
                1 => profile.faceShapeId,
                2 => profile.skinToneId,
                3 => profile.eyebrowId,
                4 => profile.eyeColorId,
                5 => profile.hairStyleId,
                _ => profile.hairColorId,
            };
        }

        private void SetValueForCategory(int index, int value)
        {
            switch (index)
            {
                case 0: profile.bodyType = value == 2 ? "FEMININE" : "MASCULINE"; break;
                case 1: profile.faceShapeId = value; break;
                case 2: profile.skinToneId = value; break;
                case 3: profile.eyebrowId = value; break;
                case 4: profile.eyeColorId = value; break;
                case 5: profile.hairStyleId = value; break;
                case 6: profile.hairColorId = value; break;
            }
        }

        private void RefreshPreview()
        {
            if (previewAnchor == null)
                return;
            if (preview == null)
            {
                preview = ProceduralAvatarFactory.Create(profile);
                preview.transform.SetParent(previewAnchor, false);
                preview.transform.localPosition = Vector3.zero;
                preview.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                preview.transform.localScale = Vector3.one * 0.72f;
            }
            else
            {
                ProceduralAvatarFactory.Apply(preview, profile);
            }
        }

        private void RefreshLabels()
        {
            if (categoryLabel != null) categoryLabel.text = Categories[categoryIndex];
            if (valueLabel != null)
            {
                var current = ValueForCategory(categoryIndex);
                valueLabel.text = categoryIndex == 0
                    ? (current == 2 ? "여성형" : "남성형")
                    : $"{current} / {CategoryLimits[categoryIndex]}";
            }
            if (masculineButton != null) masculineButton.interactable = profile.bodyType != "MASCULINE";
            if (feminineButton != null) feminineButton.interactable = profile.bodyType != "FEMININE";
        }

        private void RefreshSaveState()
        {
            if (saveButton == null)
                return;
            var enteredName = nameInput == null ? string.Empty : nameInput.text.Trim();
            profile.displayName = enteredName;
            saveButton.interactable = enteredName.Length > 0 && profile.IsComplete;
        }

        private void SetCreatorVisible(bool visible)
        {
            creatorCanvas?.SetActive(visible);
            if (previewAnchor != null) previewAnchor.gameObject.SetActive(visible);
            if (editorEntry != null) editorEntry.SetActive(!visible && hasSavedProfile);
            if (battleCanvas != null) battleCanvas.SetActive(false);
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            ApplySafeArea(safeArea, lastSafeArea);
            ApplySafeArea(editorEntrySafeArea, lastSafeArea);
        }

        private void SetNameInput(string value)
        {
            if (nameInput != null)
                nameInput.SetTextWithoutNotify(value ?? string.Empty);
        }

        private void RefreshPreviewName()
        {
            if (previewNameText == null)
                return;
            var trimmed = nameInput == null ? string.Empty : nameInput.text.Trim();
            previewNameText.text = trimmed.Length == 0 ? "플레이어" : trimmed;
        }

        private static void ApplySafeArea(RectTransform target, Rect screenSafeArea)
        {
            if (target == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            target.anchorMin = new Vector2(screenSafeArea.xMin / Screen.width, screenSafeArea.yMin / Screen.height);
            target.anchorMax = new Vector2(screenSafeArea.xMax / Screen.width, screenSafeArea.yMax / Screen.height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static AvatarProfile Copy(AvatarProfile source)
        {
            return new AvatarProfile
            {
                schemaVersion = source.schemaVersion,
                displayName = source.displayName,
                bodyType = source.bodyType,
                faceShapeId = source.faceShapeId,
                skinToneId = source.skinToneId,
                eyebrowId = source.eyebrowId,
                eyeColorId = source.eyeColorId,
                hairStyleId = source.hairStyleId,
                hairColorId = source.hairColorId,
            };
        }
    }
}
