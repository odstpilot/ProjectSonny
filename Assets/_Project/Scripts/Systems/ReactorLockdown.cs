using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// reactor lockdown fight
public class ReactorLockdown : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public PlaceholderRobot[] bots = new PlaceholderRobot[0];
    }

    public PlayerController player;
    public Stairway stairs;
    [Tooltip("Where the player sets it off, in the world.")]
    public Rect wakeArea;
    [Tooltip("The blast gates that shut the player in.")]
    public ReactorGate[] blastGates = new ReactorGate[0];
    [Tooltip("The gates in the containment fence the bots come out of, and how far out in front of them they roll.")]
    public ReactorGate[] spawnGates = new ReactorGate[0];
    public Vector2 rollOut = new Vector2(0f, 1.5f);
    public Wave[] waves = new Wave[0];
    [Tooltip("Seconds before each wave, and between one bot coming out and the next.")]
    public float waveDelay = 1.6f;
    public float botGap = 0.45f;

    [TextArea] public string[] technicianOnLockdown = { "Uhh... why are the alarms going" };
    [TextArea] public string[] technicianPipGone = { "Pip?... Pip, come on... damn it" };
    [TextArea] public string[] technicianCleared = { "That's all of them... the gates are up, stairs, go" };
    public string stairsLockedLine = "Stairs are locked down... not till this lockdown lifts";
    [SoundName] public string alarmSound = "Alarm";
    [Header("Testing")]
    [Tooltip("Where the player stands for the Checkpoint Tester: a step short of setting it off, and by the stairs once it's beaten.")]
    public Vector2 standAt;
    public Vector2 clearedStandAt;

    private int wave;
    private bool running, done;
    private Coroutine lockdown;
    private Light2D beacon;
    private Health playerHealth;
    private readonly HashSet<PlaceholderRobot> released = new HashSet<PlaceholderRobot>();
    private readonly Dictionary<PlaceholderRobot, Vector3> startAt = new Dictionary<PlaceholderRobot, Vector3>();

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player != null && player.TryGetComponent(out playerHealth)) playerHealth.Died += OnPlayerDied;
        if (stairs != null)
        {
            stairs.locked = true;
            stairs.lockedLine = stairsLockedLine;
        }
        foreach (Wave w in waves)
            foreach (PlaceholderRobot bot in w.bots)
                if (bot != null)
                {
                    startAt[bot] = bot.transform.position;
                    bot.gameObject.SetActive(false);
                }

        var holder = new GameObject("Alarm Beacon");
        holder.transform.SetParent(transform, false);
        holder.transform.position = wakeArea.center;
        beacon = holder.AddComponent<Light2D>();
        beacon.lightType = Light2D.LightType.Point;
        beacon.color = new Color(1f, 0.15f, 0.08f);
        beacon.pointLightOuterRadius = Mathf.Max(wakeArea.width, wakeArea.height) * 0.7f;
        beacon.pointLightInnerAngle = 30f;
        beacon.pointLightOuterAngle = 70f;
        beacon.falloffIntensity = 0.5f;
        beacon.shadowsEnabled = false;
        beacon.intensity = 0f;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
    }

    void Update()
    {
        if (beacon != null)
        {
            beacon.intensity = Mathf.MoveTowards(beacon.intensity, running ? 1.4f : 0f, Time.deltaTime * 3f);
            beacon.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 240f);
        }
        if (done || running || player == null || (playerHealth != null && playerHealth.IsDead)) return;
        if (wakeArea.Contains((Vector2)player.transform.position)) lockdown = StartCoroutine(Lockdown());
    }

    IEnumerator Lockdown()
    {
        running = true;
        Sfx.Play(alarmSound);
        foreach (ReactorGate gate in blastGates) if (gate != null) gate.Close();
        TechnicianVoice.Hush();
        if (wave == 0) StartCoroutine(TechnicianVoice.Think(technicianOnLockdown));
        if (SuitHelper.Exists && wave == 0 && PipManager.Lines.lockdownStarts.Length > 0) SuitHelper.Get().Tell(PipManager.Lines.lockdownStarts);

        for (; wave < waves.Length; wave++)
        {
            yield return new WaitForSeconds(waveDelay);
            if (wave > 0 && SuitHelper.Exists && PipManager.Lines.lockdownNextWave.Length > 0)
                SuitHelper.Get().Tell(new[] { PipManager.Lines.lockdownNextWave[Mathf.Min(wave - 1, PipManager.Lines.lockdownNextWave.Length - 1)] });
            if (wave > 0) Sfx.Play(alarmSound, 0.7f);

            var coming = new List<PlaceholderRobot>();
            foreach (PlaceholderRobot bot in waves[wave].bots)
                if (bot != null && !released.Contains(bot)) coming.Add(bot);
            for (int i = 0; i < coming.Count; i++)
            {
                ReactorGate gate = spawnGates.Length > 0 ? spawnGates[i % spawnGates.Length] : null;
                yield return RollOut(coming[i], gate);
                yield return new WaitForSeconds(botGap);
            }
            foreach (ReactorGate gate in spawnGates) if (gate != null) gate.Close();
            while (System.Array.Exists(waves[wave].bots, b => b != null && b.gameObject.activeInHierarchy && !b.GetComponent<Health>().IsDead))
                yield return null;
        }

        running = false;
        done = true;
        foreach (ReactorGate gate in blastGates) if (gate != null) gate.Open();
        if (stairs != null) stairs.locked = false;
        yield return new WaitForSeconds(0.6f);
        yield return SuitHelper.Get().Die(PipManager.Lines.lastWords);
        yield return new WaitForSeconds(0.8f);
        yield return TechnicianVoice.Think(technicianPipGone);
        yield return TechnicianVoice.Think(technicianCleared);
    }

    IEnumerator RollOut(PlaceholderRobot bot, ReactorGate gate)
    {
        Vector3 from = gate != null ? gate.transform.position : startAt[bot];
        Vector3 to = from + (Vector3)rollOut;
        if (gate != null)
        {
            gate.Open();
            while (!gate.IsOpen) yield return null;
        }
        released.Add(bot);
        bot.transform.position = new Vector3(from.x, from.y, startAt[bot].z);
        Collider2D[] solids = bot.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D c in solids) c.enabled = false;
        bot.gameObject.SetActive(true);
        HitEffects.Sparks(from, Vector2.up, 8, 3f, 120f, Color.white, new Color(1f, 0.7f, 0.2f));
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (bot == null) yield break;
            Vector3 at = Vector3.Lerp(from, to, t / 0.5f);
            bot.transform.position = new Vector3(at.x, at.y, startAt[bot].z);
            if (bot.TryGetComponent(out Rigidbody2D body)) body.position = at;
            yield return null;
        }
        if (bot == null) yield break;
        foreach (Collider2D c in solids) if (c != null) c.enabled = true;
        if (player != null) bot.Alarm(player.transform.position);
    }

    public void SkipToCleared()
    {
        if (lockdown != null) StopCoroutine(lockdown);
        lockdown = null;
        running = false;
        done = true;
        foreach (Wave w in waves)
            foreach (PlaceholderRobot bot in w.bots)
                if (bot != null) Destroy(bot.gameObject);
        foreach (ReactorGate gate in blastGates) if (gate != null) gate.Open();
        foreach (ReactorGate gate in spawnGates) if (gate != null) gate.Close();
        if (stairs != null) stairs.locked = false;
        SuitHelper.Wear = SuitHelper.Condition.Gone;
        if (SuitHelper.Exists) StartCoroutine(SuitHelper.Get().GoOffline(true));
    }

    void OnPlayerDied()
    {
        if (!running) return;
        if (lockdown != null) StopCoroutine(lockdown);
        lockdown = null;
        running = false;
        for (int w = wave; w < waves.Length; w++)
            foreach (PlaceholderRobot bot in waves[w].bots)
            {
                if (bot == null || bot.GetComponent<Health>().IsDead) continue;
                released.Remove(bot);
                bot.gameObject.SetActive(false);
            }
        foreach (ReactorGate gate in spawnGates) if (gate != null) gate.Close();
        foreach (ReactorGate gate in blastGates) if (gate != null) gate.Open();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f);
        Gizmos.DrawWireCube(wakeArea.center, wakeArea.size);
    }
}
