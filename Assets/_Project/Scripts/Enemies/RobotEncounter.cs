using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// A group of robots the level treats as one fight. It can keep them powered down, dark and still, until the player
// arrives, then wake them all at once; hitting a powered-down robot wakes the group early. It reports when the last one
// is destroyed, for instance to open the way on.
// Put it on an empty object and list the robots, or leave the list empty to use every robot under this object.
public class RobotEncounter : MonoBehaviour
{
    const float WakeBlinkTime = 0.06f;
    const int WakeBlinks = 6;
    static readonly Color DormantTint = new Color(0.35f, 0.37f, 0.42f);

    [Tooltip("Leave empty to use every robot (anything with Health) under this object.")]
    public List<Health> robots = new List<Health>();
    [Tooltip("Robots stand dark and still until Activate is called, or until one of them is hit.")]
    public bool startDormant = true;
    public AudioClip wakeClip;
    [Range(0f, 1f)] public float wakeVolume = 0.6f;
    public UnityEvent onCleared = new UnityEvent();

    public event System.Action Cleared;

    public bool IsActive { get; private set; }
    public bool IsCleared { get; private set; }
    public int Remaining { get; private set; }

    // The scripts switched off on each dormant robot, so exactly those come back on.
    private readonly List<Behaviour> sleeping = new List<Behaviour>();
    private readonly Dictionary<SpriteRenderer, Color> litColors = new Dictionary<SpriteRenderer, Color>();

    void Awake()
    {
        if (robots.Count == 0) robots.AddRange(GetComponentsInChildren<Health>());
        robots.RemoveAll(robot => robot == null);

        Remaining = robots.Count;
        IsCleared = Remaining == 0;
        IsActive = !startDormant;

        foreach (Health robot in robots)
        {
            robot.Died += OnRobotDied;
            robot.Damaged += OnRobotDamaged;
            if (startDormant) PowerDown(robot);
        }
    }

    void Start()
    {
        // Tinted here rather than in Awake, after the robots have remembered their real colors.
        if (IsActive) return;
        foreach (Health robot in robots)
        {
            foreach (SpriteRenderer sprite in robot.GetComponentsInChildren<SpriteRenderer>())
                litColors[sprite] = sprite.color;
        }
        SetDark(true);
    }

    void OnDestroy()
    {
        foreach (Health robot in robots)
        {
            if (robot == null) continue;
            robot.Died -= OnRobotDied;
            robot.Damaged -= OnRobotDamaged;
        }
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        StartCoroutine(WakeUp());
    }

    IEnumerator WakeUp()
    {
        if (wakeClip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(wakeClip, Camera.main.transform.position, wakeVolume);

        // Their lights stutter on before they move.
        for (int i = 0; i < WakeBlinks; i++)
        {
            SetDark(i % 2 == 1);
            yield return new WaitForSeconds(WakeBlinkTime);
        }
        SetDark(false);

        foreach (Behaviour behaviour in sleeping)
            if (behaviour != null) behaviour.enabled = true;
        sleeping.Clear();
    }

    // Everything on the robot but its health and hit effects stops, so it can still be hurt while it waits.
    void PowerDown(Health robot)
    {
        foreach (MonoBehaviour behaviour in robot.GetComponents<MonoBehaviour>())
        {
            if (!behaviour.enabled || behaviour is Health || behaviour is DamageEffects) continue;
            behaviour.enabled = false;
            sleeping.Add(behaviour);
        }
        if (robot.TryGetComponent(out Rigidbody2D body))
            body.linearVelocity = Vector2.zero;
    }

    void SetDark(bool dark)
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in litColors)
        {
            if (pair.Key == null) continue;
            Color lit = pair.Value;
            pair.Key.color = dark ? new Color(lit.r * DormantTint.r, lit.g * DormantTint.g, lit.b * DormantTint.b, lit.a) : lit;
        }
    }

    void OnRobotDamaged(DamageInfo info)
    {
        Activate();
    }

    void OnRobotDied()
    {
        Remaining = Mathf.Max(0, Remaining - 1);
        if (Remaining > 0 || IsCleared) return;

        IsCleared = true;
        Cleared?.Invoke();
        onCleared.Invoke();
    }
}
