# ManaMune

Scales your character's breast bones with how much mana you have left.

Full mana gives one scale, empty mana gives another, and it slides linearly
between the two as you cast. Requires [Customize+](https://github.com/Aether-Tools/CustomizePlus).

## Install

Dalamud → `/xlsettings` → **Experimental** → *Custom Plugin Repositories*, add:

```
https://raw.githubusercontent.com/Noffletoff/mana-mune/main/repo.json
```

## Use

`/manamune` opens the window. `/manamune on` and `/manamune off` toggle it
without opening anything.

- **At full mana / At empty mana** — the two ends of the mapping. They are
  *multipliers* on whatever your own Customize+ profile already sets, so a
  1.4 shape at half mana becomes 1.4 × 0.8, not 0.8.
- **Invert** — swap the ends. Small when full, large when empty.
- **Only on jobs that spend mana** — see below.
- **While dead** — dying empties the mana bar, so *Keep following mana* pins
  every corpse at the minimum size. *Turn off* (the default) shows your own
  Customize+ profile instead; *Freeze at last size* keeps whatever size you
  died at. Either way it resumes the moment you are raised, at whatever mana
  you come back with — so expect to return small and grow as you regen.
- **Bones** — vanilla `j_mune_l` / `j_mune_r` by default, with IVCS and
  pectoral bones as tick boxes and a free-text field for anything else.

## How it works, and the one thing worth knowing

Customize+ applies exactly **one** profile per character, and a *temporary*
profile — the kind a plugin can push over IPC — outranks the rest. So ManaMune
cannot simply send "the breast bones at 0.8"; that would replace your profile
and silently drop every other bone you have edited.

Instead it reads the profile you are actually wearing, copies it, multiplies the
mana factor into the breast bones of that copy, and sends the result. Everything
else you have scaled comes along untouched. The status line names the profile it
found and how many bones it is carrying, so you can see this working.

The catch: while ManaMune is applying, Customize+ reports *ManaMune's* profile
as the active one, so a profile switch cannot be noticed automatically. Zoning,
changing job, and logging in all re-detect, and there is a **Re-detect profile**
button for when you switch profiles standing still.

## Jobs without mana

Since Endwalker every job reports 10000 MP, so the game cannot be asked whether
a job has a mana bar — a Samurai simply sits at 100% forever. Left alone that
would pin you at the full-mana scale while still holding a temporary profile in
front of your own one. **Only on jobs that spend mana** (on by default) makes
ManaMune withdraw entirely on those jobs instead. The list is
PLD, DRK, WHM, SCH, AST, SGE, BLM, SMN, RDM, BLU, PCT and their base classes.

## What it deliberately does not do

- **No skeleton hooking.** Everything goes through the Customize+ IPC. Writing
  bone transforms directly is how plugins crash the game on patch day.
- **Local only.** Temporary profiles are not synced, so nobody else sees this —
  not through Mare, not through anything. It is for your screen.
- **You only.** Not party members, not passers-by.
- **Never zero.** Scale is floored at 0.05. A zero-scaled bone collapses
  everything parented to it.

## Development

```bash
dotnet test ManaMune.Tests/ManaMune.Tests.csproj
```

The mapping, the job list and the profile merge are pure functions with no
Dalamud types in them, linked into the test project rather than referenced, so
the suite runs with the game closed and Dalamud absent. The parts that cannot be
tested that way — the IPC call gates — were written against signatures read out
of the installed `CustomizePlus.dll` by reflection, not from documentation.

Build and install as a dev plugin:

```bash
dotnet build ManaMune/ManaMune.csproj -c Release
```

then copy `ManaMune.dll`, `ManaMune.json` and `ManaMune.deps.json` from
`ManaMune/bin/Release/` into `%APPDATA%\XIVLauncher\devPlugins\ManaMune\`.
