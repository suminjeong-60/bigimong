#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Bigimong.AR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class AvatarCreatorEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            VerifyCatalogAndWrapping();
            VerifyFactoryGeometry();
            VerifyCreatorLifecycle();
        }

        public static void RunSceneChecks()
        {
            VerifySceneLayout();
        }

        private static void VerifyCatalogAndWrapping()
        {
            AssertEqual(8, AvatarCustomizationCatalog.SkinTones.Length, "skin tone count");
            AssertEqual(8, AvatarCustomizationCatalog.EyeColors.Length, "eye color count");
            AssertEqual(10, AvatarCustomizationCatalog.HairColors.Length, "hair color count");
            AssertEqual(AvatarCustomizationCatalog.SkinTones[0], AvatarCustomizationCatalog.ColorForSkinTone(0), "skin lower clamp");
            AssertEqual(AvatarCustomizationCatalog.EyeColors[7], AvatarCustomizationCatalog.ColorForEye(99), "eye upper clamp");
            AssertEqual(AvatarCustomizationCatalog.HairColors[9], AvatarCustomizationCatalog.ColorForHair(99), "hair upper clamp");

            var expectedLimits = new[] { 2, 5, 8, 6, 8, 12, 10 };
            AssertEqual(expectedLimits.Length, AvatarCreatorController.CategoryCount, "category count");
            for (var category = 0; category < expectedLimits.Length; category++)
            {
                var limit = expectedLimits[category];
                AssertEqual(limit, AvatarCreatorController.CategoryLimit(category), $"category {category} limit");
                AssertEqual(limit, AvatarCreatorController.WrapSelection(1, -1, limit), $"category {category} previous wrap");
                AssertEqual(1, AvatarCreatorController.WrapSelection(limit, 1, limit), $"category {category} next wrap");
            }
        }

        private static void VerifyFactoryGeometry()
        {
            for (var faceShapeId = 1; faceShapeId <= 5; faceShapeId++)
            for (var eyebrowId = 1; eyebrowId <= 6; eyebrowId++)
            {
                var root = ProceduralAvatarFactory.Create(new AvatarProfile { faceShapeId = faceShapeId, eyebrowId = eyebrowId });
                try
                {
                    AssertEqual(0, root.GetComponentsInChildren<Collider>(true).Length, "cosmetic collider count");
                    AssertSurfaceOverlap(root.transform, "Head", "EyeLeft");
                    AssertSurfaceOverlap(root.transform, "Head", "EyeRight");
                    AssertSurfaceOverlap(root.transform, "Head", "EyebrowLeft");
                    AssertSurfaceOverlap(root.transform, "Head", "EyebrowRight");
                    AssertLayerOverlap(root.transform, "EyeLeft", "IrisLeft");
                    AssertLayerOverlap(root.transform, "EyeRight", "IrisRight");
                    AssertLayerOverlap(root.transform, "IrisLeft", "PupilLeft");
                    AssertLayerOverlap(root.transform, "IrisRight", "PupilRight");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            var hairSignatures = new HashSet<string>();
            for (var hairStyleId = 1; hairStyleId <= 12; hairStyleId++)
            {
                var root = ProceduralAvatarFactory.Create(new AvatarProfile { hairStyleId = hairStyleId });
                try
                {
                    var hairRoot = RequiredChild(root.transform, "HairRoot");
                    var signature = string.Empty;
                    for (var index = 0; index < hairRoot.childCount; index++)
                        signature += hairRoot.GetChild(index).name + ";";
                    AssertTrue(hairSignatures.Add(signature), $"hair style {hairStyleId} must have a distinct assembly");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void VerifyCreatorLifecycle()
        {
            var root = new GameObject("Avatar Creator Check Root");
            try
            {
                var controller = root.AddComponent<AvatarCreatorController>();
                var creatorCanvas = Child(root.transform, "Creator Canvas");
                var battleCanvas = Child(root.transform, "Battle Canvas");
                var editorEntry = Child(root.transform, "Editor Entry");
                var previewAnchor = Child(root.transform, "Preview Anchor").transform;
                var safeArea = RectChild(root.transform, "Safe Area");
                var editorSafeArea = RectChild(root.transform, "Editor Safe Area");
                var input = InputChild(root.transform, "Name Input");
                var previewName = TextChild(root.transform, "Preview Name");
                var category = TextChild(root.transform, "Category");
                var value = TextChild(root.transform, "Value");
                var previous = ButtonChild(root.transform, "Previous");
                var next = ButtonChild(root.transform, "Next");
                var masculine = ButtonChild(root.transform, "Masculine");
                var feminine = ButtonChild(root.transform, "Feminine");
                var save = ButtonChild(root.transform, "Save");
                var edit = ButtonChild(root.transform, "Edit");

                SetField(controller, "creatorCanvas", creatorCanvas);
                SetField(controller, "battleCanvas", battleCanvas);
                SetField(controller, "editorEntry", editorEntry);
                SetField(controller, "safeArea", safeArea);
                SetField(controller, "editorEntrySafeArea", editorSafeArea);
                SetField(controller, "previewAnchor", previewAnchor);
                SetField(controller, "nameInput", input);
                SetField(controller, "previewNameText", previewName);
                SetField(controller, "categoryLabel", category);
                SetField(controller, "valueLabel", value);
                SetField(controller, "previousButton", previous);
                SetField(controller, "nextButton", next);
                SetField(controller, "masculineButton", masculine);
                SetField(controller, "feminineButton", feminine);
                SetField(controller, "saveButton", save);
                SetField(controller, "editButton", edit);
                SetField(controller, "categoryButtons", Array.Empty<Button>());

                var savedProfile = new AvatarProfile { displayName = "  Mina  ", hairStyleId = 12 };
                controller.Initialize(savedProfile, true);
                AssertFalse(creatorCanvas.activeSelf, "saved profile must skip creator");
                AssertFalse(previewAnchor.gameObject.activeSelf, "saved profile must hide preview");
                AssertFalse(battleCanvas.activeSelf, "saved profile must wait for the beta phase flow");
                AssertTrue(editorEntry.activeSelf, "saved profile must expose editor entry");

                controller.OpenEditor(savedProfile);
                AssertTrue(creatorCanvas.activeSelf, "editor must reopen creator");
                AssertTrue(previewAnchor.gameObject.activeSelf, "editor must restore preview");
                AssertFalse(battleCanvas.activeSelf, "opening editor must keep battle hidden");
                AssertFalse(editorEntry.activeSelf, "editor entry must hide while editing");
                AssertEqual("Mina", input.text, "editor name");
                AssertEqual("BODY", category.text, "editor category reset");
                AssertEqual("Mina", previewName.text, "editor preview name");

                controller.SelectCategory(5);
                controller.Next();
                AssertEqual("HAIR", category.text, "selected category label");
                AssertEqual("1 / 12", value.text, "hair selection wrap");

                controller.Initialize(AvatarProfile.CreateDefault(), false);
                AssertTrue(creatorCanvas.activeSelf, "first launch must show creator");
                AssertTrue(previewAnchor.gameObject.activeSelf, "first launch must show preview");
                AssertFalse(battleCanvas.activeSelf, "first launch must keep battle hidden");
                AssertFalse(editorEntry.activeSelf, "first launch must not show duplicate editor entry");
                AssertEqual(string.Empty, input.text, "first launch name input");
                AssertEqual("플레이어", previewName.text, "first launch preview label");
                AssertFalse(save.interactable, "first launch blank name cannot save");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifySceneLayout()
        {
            var scrollObject = RequiredSceneObject("Avatar Creator Scroll View");
            var scroll = scrollObject.GetComponent<ScrollRect>();
            AssertTrue(scroll != null, "creator controls require a ScrollRect");
            AssertTrue(scroll.vertical && !scroll.horizontal, "creator controls must scroll vertically only");
            AssertTrue(scroll.viewport != null && scroll.content != null, "scroll view needs viewport and content");
            var viewportGraphic = scroll.viewport.GetComponent<Graphic>();
            AssertTrue(viewportGraphic != null && viewportGraphic.raycastTarget, "scroll viewport needs a touch raycast surface");

            var save = RequiredSceneObject("Save Avatar").GetComponent<RectTransform>();
            AssertTrue(save != null, "save control needs a RectTransform");
            var saveBottom = save.anchoredPosition.y - save.sizeDelta.y * 0.5f;
            AssertTrue(saveBottom >= -scroll.content.rect.height, "save control must remain inside scroll content");
            AssertTrue(scroll.content.rect.height >= 700f, "content must scroll when the safe viewport is short");
            AssertAbove(RequiredSceneObject("Avatar Name").GetComponent<RectTransform>(),
                RequiredSceneObject("Category").GetComponent<RectTransform>(), "name and category");
            AssertAbove(RequiredSceneObject("Category").GetComponent<RectTransform>(),
                RequiredSceneObject("Category 1").GetComponent<RectTransform>(), "category and tabs");
            AssertAbove(RequiredSceneObject("Category 1").GetComponent<RectTransform>(),
                RequiredSceneObject("Selection").GetComponent<RectTransform>(), "tabs and selection");
            AssertAbove(RequiredSceneObject("Selection").GetComponent<RectTransform>(),
                RequiredSceneObject("Previous").GetComponent<RectTransform>(), "selection and arrows");
            AssertAbove(RequiredSceneObject("Previous").GetComponent<RectTransform>(),
                RequiredSceneObject("Masculine Body").GetComponent<RectTransform>(), "arrows and body buttons");
            AssertAbove(RequiredSceneObject("Masculine Body").GetComponent<RectTransform>(), save, "body buttons and save");

            AssertTrue(RequiredSceneObject("Edit Avatar").GetComponent<Button>() != null, "saved avatars need an edit button");
            AssertFalse(RequiredSceneObject("AR Battle HUD").activeSelf, "scene must not activate battle before phase advance");
        }

        private static void AssertSurfaceOverlap(Transform root, string surfaceName, string featureName)
        {
            var surface = RequiredChild(root, surfaceName);
            var feature = RequiredChild(root, featureName);
            var offset = feature.localPosition - surface.localPosition;
            var radiusX = surface.localScale.x * 0.5f;
            var radiusY = surface.localScale.y * 0.5f;
            var radiusZ = surface.localScale.z * 0.5f;
            var normalized = 1f - offset.x * offset.x / (radiusX * radiusX) - offset.y * offset.y / (radiusY * radiusY);
            AssertTrue(normalized >= 0f, $"{featureName} center must be inside the {surfaceName} silhouette");
            var surfaceDepth = surface.localPosition.z + radiusZ * Mathf.Sqrt(normalized);
            var featureRadius = feature.localScale.z * 0.5f;
            AssertTrue(feature.localPosition.z - featureRadius < surfaceDepth, $"{featureName} must overlap {surfaceName}");
            AssertTrue(feature.localPosition.z + featureRadius > surfaceDepth, $"{featureName} must remain visible above {surfaceName}");
        }

        private static void AssertLayerOverlap(Transform root, string backName, string frontName)
        {
            var back = RequiredChild(root, backName);
            var front = RequiredChild(root, frontName);
            var backFront = back.localPosition.z + back.localScale.z * 0.5f;
            var frontBack = front.localPosition.z - front.localScale.z * 0.5f;
            AssertTrue(frontBack < backFront, $"{frontName} must overlap {backName}");
            AssertTrue(front.localPosition.z > back.localPosition.z, $"{frontName} must face forward from {backName}");
        }

        private static void AssertAbove(RectTransform upper, RectTransform lower, string label)
        {
            var upperBottom = upper.anchoredPosition.y - upper.rect.height * 0.5f;
            var lowerTop = lower.anchoredPosition.y + lower.rect.height * 0.5f;
            AssertTrue(upperBottom >= lowerTop, $"Scrollable controls overlap: {label}");
        }

        private static GameObject RequiredSceneObject(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var match = FindRecursive(root.transform, name);
                if (match != null)
                    return match.gameObject;
            }
            throw new InvalidOperationException($"Scene object '{name}' was not created.");
        }

        private static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name)
                return current;
            for (var index = 0; index < current.childCount; index++)
            {
                var match = FindRecursive(current.GetChild(index), name);
                if (match != null)
                    return match;
            }
            return null;
        }

        private static Transform RequiredChild(Transform root, string name)
        {
            var child = root.Find(name);
            return child != null ? child : throw new InvalidOperationException($"Missing avatar part '{name}'.");
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform RectChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Text TextChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Text>();
        }

        private static Button ButtonChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Button>();
        }

        private static InputField InputChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            child.transform.SetParent(parent, false);
            var text = TextChild(child.transform, "Text");
            var input = child.GetComponent<InputField>();
            input.textComponent = text;
            return input;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Missing serialized field '{name}'.");
            field.SetValue(target, value);
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool value, string message) => AssertTrue(!value, message);

        private static void AssertEqual<T>(T expected, T actual, string field)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"Unexpected {field}: expected '{expected}', got '{actual}'.");
        }
    }
}
#endif
