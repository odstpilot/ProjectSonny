using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class PauseSettings : MonoBehaviour
{
    public RectTransform screen;
    public Font font;

    public GameObject pauseMenuUI;
    public GameObject settingsButton;
    public GameObject controlsButton;

    private TMP_FontAsset fontAsset;
    private AudioSource sfx;
    private TitleSettings settings;
    private MenuInput input;

    private bool wasOpen = false;
    private int closedFrame = -1;

    private GameObject returnButton;

    public bool IsOpen
    {
        get
        {
            return settings != null && settings.IsOpen;
        }
    }

    public bool ClosedThisFrame
    {
        get
        {
            return Time.frameCount == closedFrame;
        }
    }

    void Start()
    {
        fontAsset = TitleUI.MakeFontAsset(font);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        AudioClip moveClip =
            TitleUI.Tone("PauseMove", 990f, 0.06f, 0.35f);

        AudioClip selectClip =
            TitleUI.Tone("PauseSelect", 1320f, 0.09f, 0.45f);

        AudioClip backClip =
            TitleUI.Tone("PauseBack", 620f, 0.08f, 0.4f);

        settings = new TitleSettings(
            screen,
            fontAsset,
            sfx,
            moveClip,
            selectClip,
            backClip
        );

        input = new MenuInput();
    }

    void Update()
    {
        if (settings == null)
            return;

        float now = Time.unscaledTime;

        if (settings.IsOpen)
        {
            input.Read(now);
            settings.Tick(now, input);
        }

        settings.Animate(now);

        if (wasOpen && !settings.IsOpen)
        {
            closedFrame = Time.frameCount;

            pauseMenuUI.SetActive(true);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(returnButton);
            }
        }

        wasOpen = settings.IsOpen;
    }

    public void OpenSettings()
    {
        if (settings == null)
            return;

        returnButton = settingsButton;

        pauseMenuUI.SetActive(false);
        settings.Open(Time.unscaledTime, 0);

        wasOpen = true;
    }

    public void OpenControls()
    {
        if (settings == null)
            return;

        returnButton = controlsButton;

        pauseMenuUI.SetActive(false);
        settings.Open(Time.unscaledTime, 3);

        wasOpen = true;
    }
}