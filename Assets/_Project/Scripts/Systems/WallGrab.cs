using System.Collections;
using UnityEngine;

// A converted bot waiting switched off behind a wall that bursts out and grabs the player as they come near (Chapter 2,
// by the maintenance deck's stairs). It pins them where they stand, squeezing (a point of damage every so often while
// they don't fight it), until E has been pressed enough times to tear free. Torn free, it's flung back against the wall
// and the ceiling over the breach comes down on it. Once only, and only while the objective is afterObjective (if set).
// The same grab as the tutorial's (TutorialDirector.Grab), on its own. Built by ChapterOneBuilder for ChapterTwoBuilder.
public class WallGrab : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The bot, switched off behind the wall until it bursts out.")]
    public Health robot;
    [Tooltip("Where on the wall it bursts through.")]
    public Transform breachPoint;
    [Tooltip("Where the player sets it off: within triggerRange of here.")]
    public Vector2 triggerAt;
    public float triggerRange = 3f;
    [Tooltip("Only while this is the objective. Empty for any time.")]
    public string afterObjective = "";

    [Tooltip("Presses of E it takes to break free.")]
    public int breakFreePresses = 8;
    [Tooltip("Seconds without a press before it squeezes for a point of damage.")]
    public float squeezeEvery = 1.4f;
    [TextArea] public string[] technicianGrabbed = { "Get OFF me!" };
    [TextArea] public string[] technicianFree = { "...They're in the walls." };
    [TextArea] public string[] pipFree = { "~Keep moving, Tech. Stairs!" };

    // While it has hold of them, for anything that shouldn't happen meanwhile.
    public static bool Holding { get; private set; }

    private bool sprung;

    void OnDisable() => Holding = false;

    void Update()
    {
        if (sprung || player == null || robot == null) return;
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || VentNetwork.IsPlayerInside) return;
        if (!string.IsNullOrEmpty(afterObjective) && TutorialHud.Get().Objective != afterObjective) return;
        if (Vector2.Distance(player.transform.position, triggerAt) > triggerRange) return;
        sprung = true;
        StartCoroutine(Grab());
    }

    IEnumerator Grab()
    {
        Holding = true;
        Transform target = player.transform;
        TutorialHud hud = TutorialHud.Get();
        if (breachPoint != null) TutorialSetPieces.BlowInWall(breachPoint.position);
        robot.gameObject.SetActive(true);
        var brain = robot.GetComponent<PlaceholderRobot>();
        if (brain != null) brain.enabled = false;
        var body = robot.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        player.SetScriptedInput(Vector2.zero, false);
        HitStop.Hold(0.3f, 0.25f);

        // A leap, onto them.
        Vector2 from = robot.transform.position;
        const float Leap = 0.3f;
        for (float t = 0f; t < Leap; t += Time.deltaTime)
        {
            float p = t / Leap;
            Vector2 onto = (Vector2)target.position + new Vector2(-0.35f, 0.25f);
            Vector2 at = Vector2.Lerp(from, onto, p) + Vector2.up * Mathf.Sin(p * Mathf.PI) * 0.8f;
            Move(at);
            yield return null;
        }
        CameraShake.Kick(Vector2.down, 0.25f);
        CameraShake.Shake(0.4f);
        if (technicianGrabbed.Length > 0) TechnicianVoice.Say(technicianGrabbed[0], 1.4f);

        hud.ShowPromptWithHint("Break free", "Press it, fast", "E");
        yield return null;      // the press that got here doesn't count
        int presses = 0;
        float lastPress = Time.time;
        while (presses < breakFreePresses)
        {
            Vector2 onto = (Vector2)target.position + new Vector2(-0.35f, 0.25f);
            Move(onto + Random.insideUnitCircle * (0.03f + 0.02f * presses));
            player.SetScriptedInput(Vector2.zero, false);
            if (Input.GetKeyDown(KeyCode.E) && Time.timeScale > 0f)
            {
                presses++;
                lastPress = Time.time;
                CameraShake.Shake(0.12f);
                CameraShake.Kick(Random.insideUnitCircle.normalized, 0.08f);
            }
            // It squeezes while they don't fight it.
            if (Time.time - lastPress > squeezeEvery)
            {
                lastPress = Time.time;
                CameraShake.Shake(0.2f);
                if (target.TryGetComponent(out Health health))
                {
                    health.TakeDamage(new DamageInfo(1f, Vector2.down, 0f, robot.gameObject, target.position));
                    if (health.IsDead) break;
                }
            }
            yield return null;
        }
        hud.CompletePrompt();

        // Torn free: flung back against the wall, and the ceiling comes down on it.
        CameraShake.Kick(Vector2.left, 0.35f);
        HitStop.Hold(0.4f, 0.2f);
        player.ClearScriptedInput();
        Holding = false;
        Vector2 back = breachPoint != null ? (Vector2)breachPoint.position + Vector2.down * 1.2f : (Vector2)robot.transform.position + Vector2.up;
        Vector2 start = robot.transform.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            Move(Vector2.Lerp(start, back, TitleUI.EaseOut(t / 0.25f)));
            yield return null;
        }
        if (body != null) body.linearVelocity = Vector2.zero;
        FallingDebris.Drop(robot.transform.position, 0f, 0.35f, 1.3f, solid: true);
        yield return new WaitForSeconds(0.45f);
        if (robot != null && !robot.IsDead) robot.Kill(gameObject);

        yield return TechnicianVoice.Think(technicianFree);
        if (SuitHelper.Exists && pipFree.Length > 0) SuitHelper.Get().Tell(pipFree);
    }

    void Move(Vector2 at)
    {
        if (robot.TryGetComponent(out Rigidbody2D body)) body.MovePosition(at);
        else robot.transform.position = at;
    }
}
