using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VitalsLine : MonoBehaviour
{
    [Header("Appearance")]
    public Color lineColor = new Color(0f, 1f, 1f, 1f);
    public float lineThickness = 3f;

    [Header("Animation")]
    public float speed = 1.5f;
    public float flatlineTime = 1.25f;

    private RectTransform rect;
    private readonly List<Image> lines = new List<Image>();

    private readonly float[] pattern =
    {
        0f, 0f, 0f,
        0.10f,
        -0.15f,
        0.75f,
        -0.55f,
        0.20f,
        0f, 0f, 0f,
        0.08f,
        0f, 0f
    };

    private float healthPercent = 1f;

    private bool flatlining = false;
    private float flatlineProgress = 0f;

    public void SetHealth(float current, float max)
    {
        if (max <= 0f)
            healthPercent = 0f;
        else
            healthPercent = Mathf.Clamp01(current / max);

        // Start the death animation.
        if (healthPercent <= 0f && !flatlining)
        {
            flatlining = true;
            flatlineProgress = 0f;
        }

        // Reset if the player has health again.
        if (healthPercent > 0f)
        {
            flatlining = false;
            flatlineProgress = 0f;
        }
    }

    void Start()
    {
        rect = GetComponent<RectTransform>();
        BuildLine();
    }

    void Update()
    {
        AnimateLine();
    }

    void BuildLine()
    {
        for (int i = 0; i < pattern.Length - 1; i++)
        {
            GameObject piece = new GameObject(
                "LinePiece",
                typeof(RectTransform),
                typeof(Image)
            );

            piece.transform.SetParent(transform, false);

            Image image = piece.GetComponent<Image>();
            image.color = lineColor;
            image.raycastTarget = false;

            lines.Add(image);
        }
    }

    void AnimateLine()
    {
        if (rect == null)
            return;

        float width = rect.rect.width;
        float height = rect.rect.height;

        if (width <= 0f || height <= 0f)
            return;

        float spacing = width / (pattern.Length - 1);
        float time = Time.unscaledTime;

        // Continue the flatline animation.
        if (flatlining)
        {
            flatlineProgress +=
                Time.unscaledDeltaTime / flatlineTime;

            flatlineProgress =
                Mathf.Clamp01(flatlineProgress);
        }

        // While dying, keep the final frantic heartbeat.
        float visualHealth = healthPercent;

        if (flatlining)
            visualHealth = 0.2f;

        float danger = 1f - visualHealth;

        float heartbeatSpeed =
            Mathf.Lerp(2f, 8f, danger);

        float movementAmount =
            Mathf.Lerp(0.02f, 0.12f, danger);

        float instability =
            Mathf.Lerp(0f, 0.18f, danger);

        float movement =
            Mathf.Sin(time * heartbeatSpeed)
            * movementAmount;

        for (int i = 0; i < lines.Count; i++)
        {
            float jitter =
                Mathf.Sin(
                    (time * heartbeatSpeed * 1.7f)
                    + (i * 2.1f)
                ) * instability;

            float nextJitter =
                Mathf.Sin(
                    (time * heartbeatSpeed * 1.7f)
                    + ((i + 1) * 2.1f)
                ) * instability;

            float extraWave = 0f;
            float nextExtraWave = 0f;

            if (visualHealth <= 0.6f)
            {
                float extraAmount =
                    Mathf.Lerp(0f, 0.25f, danger);

                extraWave =
                    Mathf.Sin(
                        (time * heartbeatSpeed * 3f)
                        + (i * 3.5f)
                    ) * extraAmount;

                nextExtraWave =
                    Mathf.Sin(
                        (time * heartbeatSpeed * 3f)
                        + ((i + 1) * 3.5f)
                    ) * extraAmount;
            }

            float y1 =
                pattern[i]
                + movement
                + jitter
                + extraWave;

            float y2 =
                pattern[i + 1]
                + movement
                + nextJitter
                + nextExtraWave;

            // -----------------------------------
            // PROGRESSIVE FLATLINE
            // -----------------------------------

            if (flatlining)
            {
                float segmentPosition =
                    (float)i / lines.Count;

                float nextSegmentPosition =
                    (float)(i + 1) / lines.Count;

                // Everything behind the advancing
                // flatline becomes completely flat.
                if (segmentPosition < flatlineProgress)
                {
                    y1 = 0f;
                }

                if (nextSegmentPosition < flatlineProgress)
                {
                    y2 = 0f;
                }
            }

            Vector2 start = new Vector2(
                -width / 2f + (i * spacing),
                y1 * height * 0.40f
            );

            Vector2 end = new Vector2(
                -width / 2f + ((i + 1) * spacing),
                y2 * height * 0.40f
            );

            PositionLine(
                lines[i].rectTransform,
                start,
                end
            );
        }
    }

    void PositionLine(
        RectTransform line,
        Vector2 start,
        Vector2 end
    )
    {
        Vector2 direction = end - start;
        float distance = direction.magnitude;

        line.anchorMin = new Vector2(0.5f, 0.5f);
        line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0.5f, 0.5f);

        line.sizeDelta =
            new Vector2(distance, lineThickness);

        line.anchoredPosition =
            (start + end) / 2f;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        line.localRotation =
            Quaternion.Euler(0f, 0f, angle);
    }
}