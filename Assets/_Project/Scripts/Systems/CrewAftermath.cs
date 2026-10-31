using System.Collections;
using UnityEngine;

// the technician finding the dead crew, and searching them
public class CrewAftermath : MonoBehaviour
{
    [System.Serializable]
    public class Body
    {
        public Transform body;
    }

    public PlayerController player;
    public Body[] bodies = new Body[0];
    [Tooltip("How close the player has to come to find one.")]
    public float findRange = 2.6f;

    [Header("Technician")]
    [Tooltip("Said near each body, one per body, in turn.")]
    [TextArea] public string[] technicianSees =
    {
        "Oh god... uhh, I was talking to them like an hour ago",
        "No no no... they didn't even make it to the door",
        "Damn... burn marks, it was the bots, it had to be",
        "I don't even know their name... I'm sorry",
        "They were just doing their job and... damn",
        "Another one... how many of them are there",
    };
    [Tooltip("How many have to be found before the technician works it out.")]
    public int realizeAfter = 3;
    [TextArea] public string[] technicianRealizes =
    {
        "It's everyone... the whole crew, and I'm the one who installed it",
    };

    [Header("Searching")]
    [Tooltip("What each body has on it, in turn, by material.")]
    public string[] lootByBody = { "Battery", "Wire Circuit", "MetalSheet", "Circuit", "Battery Wire", "MetalSheet Circuit" };
    [TextArea] public string[] technicianSearches =
    {
        "Sorry... I need this more than you do now",
        "I'll put it to use, I promise",
        "Uhh, forgive me for this",
    };

    void OnEnable()
    {
        StorageEscape.Escaped += BeginSearch;
    }

    void OnDisable()
    {
        StorageEscape.Escaped -= BeginSearch;
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i].body != null) AddLoot(bodies[i].body, i);
    }

    void AddLoot(Transform body, int index)
    {
        if (lootByBody.Length == 0) return;
        var parts = new System.Collections.Generic.List<Inventory.Material>();
        foreach (string word in lootByBody[index % lootByBody.Length].Split(' '))
            if (System.Enum.TryParse(word, out Inventory.Material material)) parts.Add(material);
        if (parts.Count == 0) return;
        var holder = new GameObject("Search");
        holder.transform.SetParent(body, false);
        holder.transform.position = body.position;
        var area = holder.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(1.4f, 0.9f);
        holder.SetActive(false);
        var loot = holder.AddComponent<BodyLoot>();
        loot.id = $"body-{index}";
        loot.parts = parts.ToArray();
        loot.interactRange = 0.7f;
        if (technicianSearches.Length > 0) loot.technicianLine = technicianSearches[index % technicianSearches.Length];
        holder.SetActive(true);
    }

    void BeginSearch() => StartCoroutine(Search());

    IEnumerator Search()
    {
        var found = new bool[bodies.Length];
        int count = 0, said = 0;
        bool realized = false;
        while (count < bodies.Length || !realized)
        {
            if (PatrolReveal.Playing)
            {
                yield return null;
                continue;
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                if (found[i] || bodies[i].body == null || player == null) continue;
                if (Vector2.Distance(player.transform.position, bodies[i].body.position) > findRange) continue;
                found[i] = true;
                count++;
                if (technicianSees.Length > 0) TechnicianVoice.Say(technicianSees[said++ % technicianSees.Length]);
            }
            if (!realized && count >= Mathf.Min(realizeAfter, bodies.Length))
            {
                yield return new WaitForSeconds(3f);
                realized = true;
                if (technicianRealizes.Length > 0) yield return TechnicianVoice.Think(technicianRealizes);
            }
            yield return null;
        }
    }
}
