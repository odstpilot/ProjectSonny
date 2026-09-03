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
`Assets/Scenes/DONT EDIT/Map.unity` that way.

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

- **Say so before editing `Map.unity`.** Smart merge helps, but two people
  restructuring the same scene still conflicts. The scene is the one file worth
  coordinating on.
- **Never commit conflict markers.** If a merge leaves `<<<<<<<` in a scene,
  Unity cannot open it. Search for `<<<<<<<` before committing.
- **Never commit `Library/`.** It is generated cache, ~2 GB, and already in
  `.gitignore`. If you see it in `git status`, something is wrong.
- **Commit the `.meta` file with the asset it belongs to.** A missing `.meta`
  makes Unity regenerate a new GUID and silently breaks every reference to that
  asset for everyone else.

## 4. If the project will not open or the console is full of errors

Delete `Library/` and reopen. It is a pure cache and Unity rebuilds it. That
fixes most "it works on my machine" import problems.

On Windows the delete can fail on `Library/PackageCache` because Unity marks
those files read-only and holds handles on them. If that happens, close Unity
and the Unity Hub first; if it still refuses, rename the folder and delete it
later — Unity only cares that `Library` is absent.

## Rollback points

- `pre-unity6` — tagged, verified-working state on Unity 2022.3.62f3, from
  before the Unity 6 upgrade.
