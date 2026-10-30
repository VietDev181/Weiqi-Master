using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Weiqi.Infrastructure;
using Weiqi.UI;

namespace Weiqi
{
    /// <summary>
    /// Entry point. Builds the whole UI (canvas, shader background, menu, game) at runtime so the scene can stay empty.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        const string MenuTrack = "spring-day-318971";

        RectTransform canvasRect;
        Material bgMat;
        MenuScreen menu;
        GameScreen gameScreen;
        CanvasGroup fadeCurtain;
        Vector2 lastSize;
        bool busy;
        float mood;
        float moodTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<App>() != null) return;
            new GameObject("WeiqiMaster").AddComponent<App>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }

            AudioService.Create();
            BuildCanvas();
            BuildEventSystem();

            menu = new MenuScreen(canvasRect);
            menu.StartRequested += cfg => StartCoroutine(GoToGame(cfg));
            gameScreen = GameScreen.Create(canvasRect);
            gameScreen.MenuRequested += () => StartCoroutine(GoToMenu());

            // fade curtain stays on top
            var curtain = UIKit.Panel(canvasRect, "Curtain", Color.black, Color.black, 0f, false);
            UIKit.Stretch((RectTransform)curtain.transform);
            fadeCurtain = curtain.gameObject.AddComponent<CanvasGroup>();
            fadeCurtain.alpha = 0f;
            fadeCurtain.blocksRaycasts = false;

            gameScreen.Group.alpha = 0f;
            gameScreen.Group.blocksRaycasts = false;
            gameScreen.Group.interactable = false;
            gameScreen.Root.gameObject.SetActive(false);

            menu.Group.alpha = 0f;
            StartCoroutine(Fade(menu.Group, 1f, 0.9f));
            AudioService.Instance.PlayMusic(MenuTrack);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)go.transform;

            var bg = UIKit.Rect("Background", canvasRect);
            UIKit.Stretch(bg);
            var img = bg.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            bgMat = new Material(UIKit.BackgroundShader) { hideFlags = HideFlags.HideAndDontSave };
            img.material = bgMat;
        }

        void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void Update()
        {
            var size = canvasRect.rect.size;
            if (size != lastSize && size.x > 1 && size.y > 1)
            {
                lastSize = size;
                bgMat.SetFloat("_Aspect", size.x / size.y);
                menu.Layout(size);
                gameScreen.Layout(size);
            }
            mood = Mathf.MoveTowards(mood, moodTarget, Time.unscaledDeltaTime * 1.2f);
            bgMat.SetFloat("_Mood", mood);
        }

        // ------------------------------------------------------------ transitions

        IEnumerator GoToGame(GameConfig cfg)
        {
            if (busy) yield break;
            busy = true;
            yield return Fade(fadeCurtain, 1f, 0.45f);
            menu.Group.alpha = 0f;
            menu.Group.blocksRaycasts = false;
            menu.Root.gameObject.SetActive(false);

            gameScreen.Root.gameObject.SetActive(true);
            gameScreen.Layout(canvasRect.rect.size);
            gameScreen.Group.alpha = 1f;
            gameScreen.Group.blocksRaycasts = true;
            gameScreen.Group.interactable = true;
            moodTarget = 1f;
            gameScreen.StartGame(cfg);
            yield return Fade(fadeCurtain, 0f, 0.6f);
            busy = false;
        }

        IEnumerator GoToMenu()
        {
            if (busy) yield break;
            busy = true;
            yield return Fade(fadeCurtain, 1f, 0.4f);
            gameScreen.Shutdown();
            gameScreen.Group.alpha = 0f;
            gameScreen.Group.blocksRaycasts = false;
            gameScreen.Root.gameObject.SetActive(false);

            menu.Root.gameObject.SetActive(true);
            menu.Group.alpha = 1f;
            menu.Group.blocksRaycasts = true;
            moodTarget = 0f;
            AudioService.Instance.PlayMusic(MenuTrack);
            yield return Fade(fadeCurtain, 0f, 0.5f);
            busy = false;
        }

        static IEnumerator Fade(CanvasGroup g, float to, float time)
        {
            float from = g.alpha;
            if (to > 0f) g.blocksRaycasts = true;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                g.alpha = Mathf.Lerp(from, to, k);
                yield return null;
            }
            g.alpha = to;
            if (g.gameObject.name == "Curtain") g.blocksRaycasts = to > 0.5f;
        }
    }
}
