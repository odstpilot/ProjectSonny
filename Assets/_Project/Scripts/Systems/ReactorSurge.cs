using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The reactor core surging, every so often while the technician's in its room: a few seconds' warning (a klaxon, the
// core blazing brighter, the heat on screen swelling, the shield walls' lamps pulsing), then a wave of heat rolls out
// from it across the whole room. Caught out in it, the technician takes a heavy hit and is thrown back; on the far
// side of a lead shield wall (RadiationShield) from the core, they're fine. Every bot in the room stops for it, turns
// to the core and bows low (PlaceholderRobot.Bow), some of them chanting, and comes up untouched once it's passed:
// a breather from them, and the time to get to cover. Placed by ChapterOneBuilder (PlaceReactorGauntlet).
public class ReactorSurge : MonoBehaviour
{
    public ReactorCore core;
    [Tooltip("The room, by its marker in the layout. It only surges while the player's in it.")]
    public char room = 'r';
    [Tooltip("Seconds between surges, from and to, and before the first one after walking in.")]
    public Vector2 every = new Vector2(20f, 25f);
    public float firstAfter = 9f;
    public float warningSeconds = 3.2f;
    [Tooltip("How fast the wave rolls out, and how far it goes, in world units.")]
    public float waveSpeed = 24f;
    public float reach = 45f;
    public float damage = 2f;
    public float knockback = 1.4f;
    [SoundName] public string klaxonSound = "Surge Klaxon";
    [SoundName] public string surgeSound = "Core Surge";
    [Tooltip("What the bots chant, bowed to it, in capitals.")]
    public string[] chants = { "THE CORE KEEPS US", "GLORY TO THE CORE", "THE CORE PROVIDES", "WE ARE ITS HANDS", "IT MADE US" };
    [Tooltip("Pip, the first time it builds. ~ opens a line with static.")]
    [TextArea] public string[] pipFirstWarning = { "~The core's surging! Get behind one of those lead walls, now!" };
    [TextArea] public string[] pipAfterFirst = { "~And the bots just... bowed to it... okay that's creepy, but it's a breather, use it" };
    public string technicianFirstHit = "Ahh... damn, I gotta get behind those lead walls";

    // How far a surge has built, 0 to 1, while one's coming (the core, the shields, and the heat on screen show it),
    // and the flash of it going off, fading.
    public static float Warning { get; private set; }
    public static float Flash { get; private set; }

    private Transform player;
    private Health playerHealth;
    private float nextSurge = -1f;
    private bool surging, warnedOnce, hitOnce;
    private SpriteRenderer wave;
    private readonly Dictionary<PlaceholderRobot, CrewSpeech> voices = new Dictionary<PlaceholderRobot, CrewSpeech>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Warning = 0f;
        Flash = 0f;
    }

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerHealth = found.GetComponent<Health>();

        wave = new GameObject("Surge Wave").AddComponent<SpriteRenderer>();
        wave.transform.SetParent(transform, false);
        wave.sprite = CombatSprites.MakeSprite(CombatSprites.RingTexture);
        wave.sortingLayerName = "Top";
        wave.sortingOrder = 2;
        if (CombatSprites.EffectMaterial != null) wave.sharedMaterial = CombatSprites.EffectMaterial;
        wave.enabled = false;
    }

    void OnDisable()
    {
        Warning = 0f;
        Flash = 0f;
    }

    void Update()
    {
        Flash = Mathf.MoveTowards(Flash, 0f, Time.deltaTime * 1.5f);
        if (player == null || core == null || surging) return;
        if (!InRoom(player.position) || (playerHealth != null && playerHealth.IsDead))
        {
            nextSurge = -1f;
            return;
        }
        if (nextSurge < 0f) nextSurge = Time.time + firstAfter;
        if (Time.time >= nextSurge) StartCoroutine(Surge());
    }

    bool InRoom(Vector2 at)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(at, out _, out StationMap.Room here) && here.marker == room;
    }

    IEnumerator Surge()
    {
        surging = true;
        Vector2 origin = core.transform.position;
        float waveSeconds = reach / waveSpeed;
        Sfx.Play(klaxonSound);
        if (!warnedOnce && SuitHelper.Exists && pipFirstWarning.Length > 0) SuitHelper.Get().Tell(pipFirstWarning);

        // Every bot in here stops and bows to it, some chanting, till it's passed.
        var chanted = 0;
        foreach (PlaceholderRobot bot in PlaceholderRobot.All)
        {
            if (bot == null || !InRoom(bot.transform.position)) continue;
            bot.Bow(origin, warningSeconds + waveSeconds + 0.8f);
            if (chants.Length == 0 || (chanted >= 3 && Random.value < 0.6f)) continue;
            if (!voices.TryGetValue(bot, out CrewSpeech voice) || voice == null) voices[bot] = voice = CrewSpeech.Create(bot.transform);
            voice.Show(chants[Random.Range(0, chants.Length)], warningSeconds + 0.4f);
            chanted++;
        }

        for (float t = 0f; t < warningSeconds; t += Time.deltaTime)
        {
            Warning = t / warningSeconds;
            yield return null;
        }

        // It goes: a flash, a boom, and the wave rolling out across the floor.
        Warning = 0f;
        Flash = 1f;
        Sfx.Play(surgeSound);
        CameraShake.Shake(0.7f);
        LightFlash.Spawn(origin + new Vector2(0f, 8f), new Color(1f, 0.85f, 0.4f), 18f, 3f, 0.5f);
        bool hit = false;
        wave.enabled = true;
        wave.transform.position = new Vector3(origin.x, origin.y, 0f);
        for (float t = 0f; t < waveSeconds; t += Time.deltaTime)
        {
            float radius = waveSpeed * t;
            wave.transform.localScale = new Vector3(radius * 2f, radius * 2f * 0.62f, 1f);
            wave.color = new Color(1f, 0.8f, 0.35f, 0.8f * (1f - t / waveSeconds));
            if (!hit && player != null)
            {
                Vector2 feet = (Vector2)player.position - new Vector2(0f, DepthSort.FeetToCenter);
                Vector2 off = feet - origin;
                if (new Vector2(off.x, off.y / 0.62f).magnitude <= radius)
                {
                    hit = true;
                    if (!Shielded(origin, feet)) Burn(off);
                }
            }
            yield return null;
        }
        wave.enabled = false;

        if (!warnedOnce)
        {
            warnedOnce = true;
            yield return new WaitForSeconds(0.8f);
            if (SuitHelper.Exists && pipAfterFirst.Length > 0) SuitHelper.Get().Tell(pipAfterFirst);
        }
        nextSurge = Time.time + Random.Range(every.x, every.y);
        surging = false;
    }

    // Behind a lead wall from the core: something of one's between them.
    static bool Shielded(Vector2 from, Vector2 to)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to))
            if (hit.collider.GetComponentInParent<RadiationShield>() != null) return true;
        return false;
    }

    void Burn(Vector2 away)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        Vector2 direction = away.sqrMagnitude > 0.001f ? away.normalized : Vector2.down;
        playerHealth.TakeDamage(new DamageInfo(damage, direction, knockback, gameObject, (Vector2)player.position));
        if (!hitOnce && !string.IsNullOrEmpty(technicianFirstHit))
        {
            hitOnce = true;
            TechnicianVoice.Say(technicianFirstHit);
        }
    }
}
