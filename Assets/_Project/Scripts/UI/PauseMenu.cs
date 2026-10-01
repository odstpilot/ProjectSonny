using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    public GameObject pauseMenuUI;
    public GameObject firstSelectedButton;
    public GameObject healthHUD;

    public PauseSettings pauseSettings;

    void Start()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        pauseMenuUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Do not let the pause menu use input while Settings is open.
        if (pauseSettings != null && pauseSettings.IsOpen)
        {
            return;
        }

        // Do not reuse the same ESC press that just closed Settings.
        if (pauseSettings != null && pauseSettings.ClosedThisFrame)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        pauseMenuUI.SetActive(true);
        healthHUD.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);

        AudioListener.pause = true;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        pauseMenuUI.SetActive(false);
        healthHUD.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        AudioListener.pause = false;
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene("Title");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}