using System.Collections;
using UnityEngine;

// Controls taught the moment they're needed, on the tutorial's prompt at the bottom of the screen (TutorialHud): it
// comes up when the thing's first worth doing, and ticks off green once it's been done. Each is taught once a play.
// Waits for the prompt to be free first, so it never talks over another one.
public static class ControlHints
{
    static bool taughtSwitching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay() => taughtSwitching = false;

    // Once there's more than one thing on the hotbar: the number keys switch between them.
    public static void TeachSwitching(GameObject player)
    {
        if (taughtSwitching || player == null) return;
        if (!player.TryGetComponent(out WeaponHotbar hotbar) || !player.TryGetComponent(out PlayerCombat combat)) return;
        if (hotbar.slots.Count < 2) return;
        taughtSwitching = true;
        TutorialHud hud = TutorialHud.Get();
        hud.StartCoroutine(Switching(hud, hotbar, combat));
    }

    static IEnumerator Switching(TutorialHud hud, WeaponHotbar hotbar, PlayerCombat combat)
    {
        yield return WhenFree(hud);
        bool switched = false;
        void Changed(WeaponData _) => switched = true;
        combat.WeaponChanged += Changed;
        string[] keys = new string[Mathf.Min(hotbar.slots.Count, 3)];
        for (int i = 0; i < keys.Length; i++) keys[i] = (i + 1).ToString();
        hud.ShowPromptWithHint("Switch what you're holding", "Or scroll the mouse wheel", keys);
        for (float t = 0f; !switched && t < 20f; t += Time.unscaledDeltaTime) yield return null;
        if (combat != null) combat.WeaponChanged -= Changed;
        if (switched) hud.CompletePrompt();
        else hud.HidePrompt();
    }

    // Until whatever prompt is up has gone.
    public static IEnumerator WhenFree(TutorialHud hud)
    {
        while (hud.PromptShowing || hud.PromptCompleting) yield return null;
    }
}
