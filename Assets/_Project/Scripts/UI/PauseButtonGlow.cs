using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PauseButtonGlow : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    private Image buttonImage;
    private Outline brightGlow;
    private Outline softGlow;

    void Start()
    {
        buttonImage = GetComponent<Image>();

        Outline[] outlines = GetComponents<Outline>();

        // Bright inner glow
        if (outlines.Length > 0)
        {
            brightGlow = outlines[0];
        }
        else
        {
            brightGlow = gameObject.AddComponent<Outline>();
        }

        // Softer outer glow
        if (outlines.Length > 1)
        {
            softGlow = outlines[1];
        }
        else
        {
            softGlow = gameObject.AddComponent<Outline>();
        }

        brightGlow.effectColor = new Color(0.2f, 1f, 1f, 1f);
        brightGlow.effectDistance = new Vector2(2f, -2f);

        softGlow.effectColor = new Color(0f, 1f, 1f, 0.35f);
        softGlow.effectDistance = new Vector2(5f, -5f);

        TurnGlowOff();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            TurnGlowOff();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        TurnGlowOn();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        TurnGlowOff();
    }

    private void TurnGlowOn()
    {
        // Slightly brighten the actual button too
        buttonImage.color = new Color(0.75f, 1f, 1f, 1f);

        brightGlow.enabled = true;
        softGlow.enabled = true;
    }

    private void TurnGlowOff()
    {
        buttonImage.color = Color.white;

        if (brightGlow != null)
            brightGlow.enabled = false;

        if (softGlow != null)
            softGlow.enabled = false;
    }
}