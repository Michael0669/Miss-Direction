using UnityEngine;
using UnityEngine.UI;

namespace MissDirection.Player
{
    /// <summary>
    /// Vision Fortunes: flips the screen (Upside Down, Mirror) and blacks it out (Blind).
    /// Flipping renders the player camera into a texture and shows it on a full-screen image
    /// with flipped UVs, which works in URP and the built-in pipeline alike.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerScreenEffects : MonoBehaviour
    {
        [SerializeField] PlayerModifiers modifiers;

        Camera cam;
        Camera presentCamera; // keeps something rendering to the screen while the view is redirected
        Canvas canvas;
        RawImage flippedView;
        Image blindPanel;
        RenderTexture viewTexture;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (modifiers == null) modifiers = GetComponentInParent<PlayerModifiers>();
            BuildOverlay();
        }

        void OnEnable()
        {
            if (modifiers != null) modifiers.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (modifiers != null) modifiers.Changed -= Refresh;
            ReleaseTexture();
        }

        void Update()
        {
            if (viewTexture != null && (viewTexture.width != Screen.width || viewTexture.height != Screen.height))
            {
                ReleaseTexture();
                Refresh();
            }
        }

        void Refresh()
        {
            if (modifiers == null || canvas == null) return;

            bool flipH = modifiers.Has(ModifierType.FlipScreenHorizontal);
            bool flipV = modifiers.Has(ModifierType.FlipScreenVertical);
            bool blind = modifiers.Has(ModifierType.Blind);

            blindPanel.enabled = blind;

            if (flipH || flipV)
            {
                EnsureTexture();
                cam.targetTexture = viewTexture;
                flippedView.texture = viewTexture;
                flippedView.uvRect = new Rect(flipH ? 1f : 0f, flipV ? 1f : 0f, flipH ? -1f : 1f, flipV ? -1f : 1f);
                flippedView.enabled = true;
                presentCamera.enabled = true;
            }
            else
            {
                flippedView.enabled = false;
                presentCamera.enabled = false;
                ReleaseTexture();
            }
        }

        void BuildOverlay()
        {
            var canvasObject = new GameObject("ScreenEffectsOverlay", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            flippedView = CreateFullScreen<RawImage>("FlippedView", canvasObject.transform);
            flippedView.raycastTarget = false;
            flippedView.enabled = false;

            blindPanel = CreateFullScreen<Image>("BlindPanel", canvasObject.transform);
            blindPanel.color = Color.black;
            blindPanel.raycastTarget = false;
            blindPanel.enabled = false;

            var presentObject = new GameObject("ScreenPresentCamera", typeof(Camera));
            presentObject.transform.SetParent(transform, false);
            presentCamera = presentObject.GetComponent<Camera>();
            presentCamera.cullingMask = 0;
            presentCamera.clearFlags = CameraClearFlags.SolidColor;
            presentCamera.backgroundColor = Color.black;
            presentCamera.depth = cam.depth + 1;
            presentCamera.enabled = false;
        }

        static T CreateFullScreen<T>(string objectName, Transform parent) where T : Graphic
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<T>();
        }

        void EnsureTexture()
        {
            if (viewTexture != null) return;
            viewTexture = new RenderTexture(Screen.width, Screen.height, 24) { name = "PlayerFlippedView" };
            viewTexture.Create();
        }

        void ReleaseTexture()
        {
            if (cam != null && cam.targetTexture == viewTexture) cam.targetTexture = null;
            if (viewTexture == null) return;
            viewTexture.Release();
            Destroy(viewTexture);
            viewTexture = null;
        }
    }
}
