using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// The sounds the game's scripts play, by the names they play them by, with the clips and volumes they started with.
// The level builders fill each scene's SoundManager from these (Fill), and the SoundManager inspector's Add Defaults
// menu does the same for any scene. Only sounds the scene doesn't list yet are added, so nothing already tuned there is
// touched. Sounds with no clips are made in code for now (or silent, for Stairs) until someone finds audio for them.
public static class SoundDefaults
{
    const string Sfx = "Assets/_Project/Audio/SFX";
    const string Magnetic = Sfx + "/Magnetic Sound fx/Wav";

    public class Entry
    {
        public string name;
        public SoundCategory category;
        public float volume = 1f;
        public bool loop;
        public string description;
        public string[] clips = new string[0];

        public Entry(string name, SoundCategory category, float volume, string description, params string[] clips)
        {
            this.name = name;
            this.category = category;
            this.volume = volume;
            this.description = description;
            this.clips = clips;
        }

        public Entry Looping()
        {
            loop = true;
            return this;
        }
    }

    const string MadeInCode = " Made in code until it has a clip.";

    // The player, in every level.
    public static readonly Entry[] Player =
    {
        new Entry("Footsteps", SoundCategory.SoundEffect, 0.3f, "The player's footsteps, one clip a step. Quieter and lower while crouching, higher while sprinting.",
            Sfx + "/footstep1.wav", Sfx + "/footstep2.wav", Sfx + "/footstep3.wav", Sfx + "/footstep4.wav"),
        new Entry("Low Health Heartbeat", SoundCategory.SoundEffect, 0.7f, "Loops, fading in while the player's health is low.", Sfx + "/heartbeat.wav").Looping(),
        new Entry("Death Hit", SoundCategory.SoundEffect, 1f, "The hit that kills the player, as they go down in slow motion.", Magnetic + "/Magnetic bass once 02.wav"),
        new Entry("Revive Static", SoundCategory.SoundEffect, 1f, "Sonny's death monitor cutting back to the game (only its first 0.6 seconds play).", Sfx + "/329782__visualasylum__radio-static.mp3"),
        new Entry("Monitor Beep", SoundCategory.UI, 0.6f, "Heart monitor beep on the death screen, one per spike." + MadeInCode),
        new Entry("Flatline", SoundCategory.UI, 0.3f, "Heart monitor flatline on the death screen, a steady tone that loops." + MadeInCode).Looping(),
    };

    public static readonly Entry[] Tutorial =
    {
        new Entry("Blast Door", SoundCategory.SoundEffect, 0.7f, "A blast door sliding open or shut. Quieter the further away it is.", Magnetic + "/Magnetic industrial layer02_1.wav"),
        new Entry("Robot Wake", SoundCategory.SoundEffect, 0.6f, "A group of robots powering on.", Sfx + "/746988__gammagool__robot-awakening-power-on (1).wav"),
        new Entry("Lamp Break", SoundCategory.SoundEffect, 0.64f, "A wall lamp smashing and cutting out. Quieter the further away it is.", Sfx + "/636578__swag1773__cutting-power.wav"),
        new Entry("Rumble", SoundCategory.SoundEffect, 0.8f, "The station shaking: a deep boom at the start of each rumble.",
            Magnetic + "/Magnetic bass ground.wav", Magnetic + "/Magnetic bass once 01.wav", Magnetic + "/Magnetic bass once 02.wav", Magnetic + "/Magnetic bass full.wav"),
        new Entry("Debris Impact", SoundCategory.SoundEffect, 0.8f, "Debris from the ceiling crashing to the floor. Quieter the further away it lands.",
            Magnetic + "/Magnetic hit 01.wav", Magnetic + "/Magnetic hit 02.wav", Magnetic + "/Magnetic hit 03.wav", Magnetic + "/Magnetic hit 04.wav"),
        new Entry("Station Hum", SoundCategory.Ambience, 0.35f, "The station's machinery, looping quietly the whole level.", Sfx + "/700008__newlocknew__scimisc_low-steady-hum-2_em.wav").Looping(),
        new Entry("Alarm", SoundCategory.SoundEffect, 0.8f, "The station alarm going off near the end.", Sfx + "/316847__lalks__alarm-04-short.wav"),
        new Entry("Heartbeat", SoundCategory.SoundEffect, 0.8f, "One loud heartbeat in the last hallway.", Sfx + "/heartbeat.wav"),
        new Entry("Ear Ringing", SoundCategory.SoundEffect, 0.12f, "High whine of ringing ears after the ceiling comes down, fading over a couple of seconds. Loops." + MadeInCode).Looping(),
        new Entry("Ending Tone", SoundCategory.SoundEffect, 0.9f, "One deep tone ringing out over the cut to black at the end. Heard over the silence." + MadeInCode),
    };

    // Sonny's processing box, in the Tutorial and Chapter 1.
    public static readonly Entry[] Sonny =
    {
        new Entry("Sonny Hum", SoundCategory.SoundEffect, 0.3f, "Sonny's box humming, looping while it's powered. Its pitch rises as it wakes.",
            Magnetic + "/Looping/Magnetic wave bass loop.wav").Looping(),
        new Entry("Sonny Awaken", SoundCategory.SoundEffect, 0.6f, "Sonny's box powering on and waking.", Magnetic + "/Magnetic bass tone 01.wav"),
    };

    // Floors built by FloorOneBuilder (Floor 1, Floor 2, Chapter 1).
    public static readonly Entry[] Floor =
    {
        new Entry("Door", SoundCategory.SoundEffect, 0.7f, "Going through a door to another room, as the screen fades to black.", Magnetic + "/Magnetic industrial layer02_1.wav"),
        new Entry("Stairs", SoundCategory.SoundEffect, 0.7f, "Taking the stairs to another floor, as the screen fades to black. Silent until it has a clip."),
    };

    public static readonly Entry[] ChapterOne =
    {
        new Entry("Entrance Door", SoundCategory.SoundEffect, 0.8f, "The ship entrance opening as the player first walks in.", Magnetic + "/Magnetic industrial layer02_1.wav"),
        new Entry("Pod Rumble", SoundCategory.SoundEffect, 1f, "Engine rumble through the arrival cutscene, as the pod docks. Loops." + MadeInCode).Looping(),
        new Entry("Docking Clunk", SoundCategory.SoundEffect, 0.9f, "The docking clamps closing on the pod." + MadeInCode),
        new Entry("Docking Hiss", SoundCategory.SoundEffect, 0.35f, "Air hissing as the pod seals to the station." + MadeInCode),
        new Entry("Dialogue Blip", SoundCategory.Voice, 0.18f, "A blip per letter as crew lines type out, pitched to each speaker's voice." + MadeInCode),
        new Entry("Dialogue Tick", SoundCategory.UI, 0.18f, "Moving between replies in a conversation." + MadeInCode),
        new Entry("Pip Chirp", SoundCategory.Voice, 0.14f, "Pip (the suit's helper) talking: a chirp every few letters, pitched a little differently each time." + MadeInCode),
        new Entry("Pip Chime", SoundCategory.UI, 0.224f, "Pip switching on for the first time." + MadeInCode),
        new Entry("Pip Static", SoundCategory.SoundEffect, 0.28f, "Static crackling over Pip's face, as if something else were on the line." + MadeInCode),
        new Entry("Install Clunk", SoundCategory.UI, 1f, "Processing core install minigame: the core seating into place." + MadeInCode),
        new Entry("Install Tick", SoundCategory.UI, 1f, "Processing core install minigame: each step ticking by." + MadeInCode),
        new Entry("Install Done", SoundCategory.UI, 1f, "Processing core install minigame: a stage done." + MadeInCode),
        new Entry("Install Spark", SoundCategory.UI, 1f, "Processing core install minigame: a miss, sparking." + MadeInCode),
        new Entry("Install Blip", SoundCategory.UI, 1f, "Processing core install minigame: a small blip." + MadeInCode),
        new Entry("Sonny Slip", SoundCategory.SoundEffect, 0.7f, "A low note under the moment Sonny's lens slips to another color." + MadeInCode),
        new Entry("Toilet Flush", SoundCategory.SoundEffect, 0.8f, "The common grounds toilet flushing, on black, during the break." + MadeInCode),
        new Entry("Toilet Gurgle", SoundCategory.SoundEffect, 0.8f, "The toilet giving out right after: a low rattle and gurgle." + MadeInCode),
        new Entry("Knock", SoundCategory.SoundEffect, 0.7f, "Knocking on an occupied restroom stall door." + MadeInCode),
        new Entry("Crate Open", SoundCategory.SoundEffect, 0.7f, "Opening the tool crate in the storage room: a latch and a lid." + MadeInCode),
        new Entry("Distant Rumble", SoundCategory.SoundEffect, 0.8f, "A far-off rumble through the station, the warning before the big one in the storage room.",
            Magnetic + "/Magnetic bass once 01.wav"),
        new Entry("Wrench Pickup", SoundCategory.SoundEffect, 0.7f, "Pulling the wrench out from under the storage room shelf: a metal clink." + MadeInCode),
        new Entry("Station Quake", SoundCategory.SoundEffect, 1f, "Something big rumbling through the whole station as the technician reaches under the shelf.",
            Magnetic + "/Magnetic bass full.wav"),
        new Entry("Shelf Rattle", SoundCategory.SoundEffect, 0.7f, "The storage room rack rocking as the technician reaches under it." + MadeInCode),
        new Entry("Knockout Hit", SoundCategory.SoundEffect, 1f, "The crate off the top of the rack landing on the technician's head.",
            Magnetic + "/Magnetic hit 01.wav", Magnetic + "/Magnetic hit 02.wav"),
    };

    // The sets offered by the inspector's Add Defaults menu.
    public static readonly (string label, Entry[][] sets)[] Menu =
    {
        ("Tutorial", new[] { Player, Tutorial, Sonny }),
        ("Chapter 1", new[] { Player, Floor, Sonny, ChapterOne }),
        ("Floor (1 or 2)", new[] { Player, Floor }),
        ("Player Only", new[] { Player }),
    };

    // The scene's SoundManager (made if it has none), with every sound in the sets it doesn't list yet added.
    // Returns how many were added.
    public static int Fill(Scene scene, params Entry[][] sets)
    {
        SoundManager manager = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<SoundManager>(true)).FirstOrDefault(found => found != null);
        if (manager == null)
        {
            var holder = new GameObject("Sound Manager");
            SceneManager.MoveGameObjectToScene(holder, scene);
            manager = holder.AddComponent<SoundManager>();
        }
        return Fill(manager, sets);
    }

    public static int Fill(SoundManager manager, params Entry[][] sets)
    {
        int added = 0;
        foreach (Entry entry in sets.SelectMany(set => set))
        {
            if (manager.sounds.Any(sound => sound.name == entry.name)) continue;
            var clips = new List<AudioClip>();
            foreach (string path in entry.clips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
                else Debug.LogWarning($"SoundDefaults: there's no audio clip at {path} for \"{entry.name}\".");
            }
            manager.sounds.Add(new Sound
            {
                name = entry.name,
                category = entry.category,
                volume = entry.volume,
                loop = entry.loop,
                description = entry.description,
                clips = clips.ToArray(),
            });
            added++;
        }
        if (added > 0) EditorUtility.SetDirty(manager);
        return added;
    }
}
