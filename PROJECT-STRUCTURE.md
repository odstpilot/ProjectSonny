# Where everything lives

Read this before you add a file. It takes two minutes and saves the next person
half an hour of hunting.

Two rules cover almost everything:

1. **Everything we make goes under `Assets/_Project/`.** Anything outside it is
   either Unity's own config or someone else's code.
2. **A `.meta` file is part of the asset.** Move it, rename it, and commit it
   together with the file it belongs to. Do this in Unity's Project window, not
   in Explorer or Finder. Losing a `.meta` gives the asset a new ID and silently
   breaks every reference to it for everyone else.

## The map

```
Assets/
  _Project/              <- everything the team makes
    Animation/
      Clips/             .anim  - individual animations
      Controllers/       .controller - state machines that play those clips
    Art/
      Characters/        Player/ and Warden/ sprite sequences, one folder per animation
      Environment/       Tilesets/ (the ship tile atlas + 471 sliced tiles), Palettes/
      Fonts/             VT323, SdAsteroidB612, and the TMP font asset
      Props/             one-off scene objects: cameras, walkie talkie, etc.
      Title/             title-screen artwork
      UI/                buttons, dialogue boxes, mute/settings icons
    Audio/
      Music/             full-length tracks
      SFX/               one-shots, footsteps, radio static, the Magnetic pack
    Prefabs/           drag these into your scene instead of rebuilding them
      Characters/        Player, Ghost, Warden, PatrolBots, WeepingAngel
      Level/             SecurityCamera, Door, Keycard, Teleporter, Alarm,
                         patrol routes, level-transition triggers
      Player/            MainCamera rig, Flashlight, CameraFlash
      Systems/           AudioPlayer, GhostManager, GlobalLight2D
      UI/                CanvasCont - the player HUD (health + death canvas)
    Scenes/
      Levels/            Map, PatrolScene            <- shipping levels
      Menus/             TitleScreen, StartScreen    <- shipping menus
      Workspaces/        one personal scene per person  <- YOUR sandbox
      _Template/         TemplateScene - the blank slate new scenes are cut from
    Scripts/
      Player/            movement, camera, flashlight, the player's HUD hooks
      Enemies/           Warden, Ghost, patrol AI, detection, security cameras
      Interactables/     doors, keycards, teleporters, guns, level triggers
      Systems/           audio, lighting, the robot quote manager
      UI/                menus and canvas controllers

  Settings/              Unity's URP render pipeline config. Unity owns this.
  ThirdParty/
    NavMeshPlus/         2D NavMesh plugin. Not ours - do not edit.
  TextMesh Pro/          package-managed. Unity owns this.
```

## Where does my new file go?

| You made a…                    | Put it in                              |
| ------------------------------ | -------------------------------------- |
| script that moves the player   | `_Project/Scripts/Player/`             |
| script that chases the player  | `_Project/Scripts/Enemies/`            |
| script for a door or pickup    | `_Project/Scripts/Interactables/`      |
| manager the whole game uses    | `_Project/Scripts/Systems/`            |
| sprite for a character         | `_Project/Art/Characters/<Character>/` |
| tile or background art         | `_Project/Art/Environment/`            |
| button, icon, or HUD image     | `_Project/Art/UI/`                     |
| sound effect                   | `_Project/Audio/SFX/`                  |
| prefab of an enemy or the player | `_Project/Prefabs/Characters/`        |
| prefab of a door, pickup, or camera | `_Project/Prefabs/Level/`          |
| scene you are just messing with | `_Project/Scenes/Workspaces/<You>.unity` |

If a file genuinely does not fit any of these, ask in chat before inventing a
new top-level folder. Folders are cheap; a second folder that means the same
thing as an existing one is not.

## Building a scene from prefabs

`_Project/Prefabs/` holds the pieces the game is made of, pulled out of
`Map.unity` so you do not have to rebuild them by hand. To put a working level
together in your workspace scene, drag in:

- `Player/MainCamera` and a `Characters/Player 1`
- `UI/CanvasCont` for the health and death UI
- `Systems/AudioPlayer` and `Systems/GlobalLight2D`
- then whatever the level needs from `Level/` - doors, keycards, cameras,
  teleporters, alarms

Editing a prefab updates every copy of it in every scene, which is the whole
point. If you need a one-off variation, right-click the prefab and make a
**Prefab Variant** rather than unpacking it - unpacking severs the link and you
stop getting everyone else's fixes.

The objects still sitting loose in `Map.unity` were deliberately left alone;
turning them into prefab instances rewrites large parts of that scene, and that
is a change worth making on its own, not bundled into a reorganization.

## Two naming rules Unity actually enforces

- **A script's filename must exactly match the class inside it.** Rename one and
  you must rename the other, or Unity detaches the script from every object
  using it. (This is why `CameraFallow.cs` still has its typo — fixing it means
  renaming the class too, which is a real change, not a cleanup.)
- **Do not rename a scene without saying so.** The name is fine to change, but
  build settings and anyone's open editor tabs point at the old one.

## Things that moved in the reorganization

Every file kept its identity, so nothing broke — but the paths changed:

| Old                                        | New                                     |
| ------------------------------------------ | --------------------------------------- |
| `Assets/*.cs` (loose at the root)          | `_Project/Scripts/<category>/`          |
| `Assets/DO NOT EDIT/Scripts/`              | `_Project/Scripts/<category>/`          |
| `Assets/DO NOT EDIT/PlugIns(…)/NavMeshComponents/` | `ThirdParty/NavMeshPlus/`       |
| `Assets/Assets/Sprites/`                   | `_Project/Art/`                         |
| `Assets/UI assets/`                        | `_Project/Art/UI/`                      |
| `Assets/Prefab/`                           | `_Project/Prefabs/`                     |
| `Assets/Scenes/DONT EDIT/`                 | `_Project/Scenes/Levels/` and `Menus/`  |
| `PotrolScene.unity`                        | `PatrolScene.unity` (typo fixed)        |
| `TemplateScene(DO NOT EDIT).unity`         | `_Template/TemplateScene.unity`         |

The old `DO NOT EDIT` folder held two unrelated things: a third-party plugin
that really should not be edited, and six of our own gameplay scripts that
people do need to read and change. They are now separate. What is genuinely
off-limits lives in `ThirdParty/`; the shared-code warning is in TEAM-SETUP.md
where people will actually read it.
