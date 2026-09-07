# Team setup

One-time steps after cloning. Takes about two minutes.

## 1. Unity version

Install **Unity 6000.6.0f1** through Unity Hub. `ProjectSettings/ProjectVersion.txt`
pins it, so the Hub will prompt you if you open the project with anything else.

Do not let Unity silently upgrade the project to a newer editor. A version bump
rewrites assets across the whole project, and if one person does it the rest of
the team can no longer open the files. If we move versions, we do it deliberately
and on a branch.

## 2. Enable Unity's scene merge tool

Scenes and prefabs are YAML. Git merges them line by line, which produces files
Unity cannot open — this repo has already had conflict markers committed into
`Map.unity` that way.

`.gitattributes` routes those files to Unity's own semantic merge tool, but git
requires the driver to be registered in each clone. Run this once, in the repo:

**Windows**

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

**macOS**

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

Adjust the path if you installed Unity somewhere else.

## 3. Working habits that avoid the painful merges

- **Work in your own scene, not in `Map.unity`.** You have a personal scene at
  `Assets/_Project/Scenes/Workspaces/<YourName>.unity`. Build and test there.
  Two people restructuring the same scene conflicts even with smart merge, and
  the shared levels are the files worth coordinating on.
- **Say so before editing anything in `Scenes/Levels/` or `Scenes/Menus/`.**
  Those are the shipping scenes. A quick "I'm in Map for the next hour" in chat
  costs nothing and prevents the merge nobody wants to untangle.
- **Read `PROJECT-STRUCTURE.md` before adding files.** It says where things go.
- **Never commit conflict markers.** If a merge leaves `<<<<<<<` in a scene,
  Unity cannot open it. Search for `<<<<<<<` before committing.
- **Never commit `Library/`.** It is generated cache, ~2 GB, and already in
  `.gitignore`. If you see it in `git status`, something is wrong.
- **Commit the `.meta` file with the asset it belongs to.** A missing `.meta`
  makes Unity regenerate a new GUID and silently breaks every reference to that
  asset for everyone else.

## 4. Your workspace scene

Everyone has their own scene under `Assets/_Project/Scenes/Workspaces/`:

    Dillon.unity    Jonathan.unity    Phi.unity
    Lulu.unity      Ryan.unity        Andrea.unity

Open yours and start building. Each one is a copy of
`Scenes/_Template/TemplateScene.unity`, so it already has a player, a camera,
lighting, the canvas, and a NavMesh surface wired up — you are not starting from
an empty scene.

**Stay in your own scene.** Not because anyone owns it, but because scenes are
the files git merges worst. Two people in separate workspace scenes never
conflict; two people in one scene almost always do.

Each workspace also has its own baked NavMesh, in the folder next to the scene
(`Workspaces/Dillon/`, and so on). That is deliberate: it means you can rebake
navigation whenever you like without overwriting anyone else's bake.

The workspace scenes are **not** in the build. They are sandboxes. When
something you built there is ready, move it into a real scene in
`Scenes/Levels/` — ideally by turning it into a prefab in
`_Project/Prefabs/`, which is far easier to merge than raw scene objects.

Need a scene for something bigger than a sandbox? Copy `_Template/TemplateScene`
rather than starting blank, and put it in `Scenes/Levels/`.

## 5. If the project will not open or the console is full of errors

Delete `Library/` and reopen. It is a pure cache and Unity rebuilds it. That
fixes most "it works on my machine" import problems.

On Windows the delete can fail on `Library/PackageCache` because Unity marks
those files read-only and holds handles on them. If that happens, close Unity
and the Unity Hub first; if it still refuses, rename the folder and delete it
later — Unity only cares that `Library` is absent.

## Rollback points

- `pre-unity6` — tagged, verified-working state on Unity 2022.3.62f3, from
  before the Unity 6 upgrade.
