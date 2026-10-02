# Catastrophe

Catastrophe is a Unity prototype about a tiny tuxedo kitten making a very large
mess in a warm living room. The goal is to complete four mischief targets by
pushing valuable objects, building chaos, and hiding before the owner catches
you.

## Current gameplay

- Start from a main menu with controls, best score, and restart flow.
- Explore a four-walled living room from a low-angle follow camera.
- Move, jump, push objects, and hide when the owner alert starts.
- Push small independent props such as mugs, tablets, books, plants, and lamps.
- Complete four targets to show a receipt-style victory bill with itemized
  damage, chaos score, grade, and saved high score.
- Fail the hide timer to show a caught panel with retry and main-menu buttons.
- Hear basic feedback sounds for paw pushes, footsteps, landings, thuds,
  fragile breaks, owner alerts, and successful hiding.
- Furniture and props remain separate GameObjects in the Unity Hierarchy so they
  can be adjusted or replaced later.

The current visual pass uses Tripo-generated furniture and props, warm wood,
white walls, colored accent pieces, dusk lighting, and a smooth 2D tuxedo kitten
sprite set that matches the reference sheets.

## Controls

| Action | Input |
| --- | --- |
| Move | `WASD` or arrow keys |
| Jump | `Space` |
| Paw push / hide | `E` |
| Pause | `Esc` |
| Camera look | Hold and drag mouse or one-finger drag |
| Camera distance | Mouse wheel or two-finger pinch |

## Requirements

- Unity `6000.6.3f1`
- Universal Render Pipeline
- Unity Input System

## Run the project

1. Clone the repository.
2. Open this repository folder as a Unity project.
3. Open `Assets/LivingRoom.unity`.
4. Press Play.

`Assets/LivingRoom.unity` is also the enabled scene in Build Settings.

## Character and scene

- The playable kitten uses directional Idle, Walk, Jump, and Push sprite
  animations built from the smooth tuxedo kitten sheets.
- Camera yaw and pitch stay within dollhouse-friendly limits so the room shell
  does not break visually.
- The room has a continuous walkable floor, four walls, window dressing, framed
  art, shelves, table props, sofa area, and warm/cool lighting balance.
- Imported Tripo assets are converted into prefabs with URP-compatible
  materials, colliders, and editable scene roots.
- Valuable objects use small horizontal nudges, damped rigidbodies, and velocity
  clamps so they slide or tip instead of flying away.

## Editor tools

Useful menu items are under `Catastrophe/` in the Unity editor:

- `Setup Smooth Sprite Cat`
- `Import All Tripo Models and Rebuild Room`
- `Fix All Tripo Materials (Textures + URP)`
- `Install LivingRoom Lighting`
- `Run Validation Batch 10`
- `Run Validation Batch 20`

## Project structure

```text
Assets/
  Art/                    Models, textures, animation sheets, and materials
  Editor/                 Scene builders and validation tools
  Models/Tripo/           Imported Tripo FBX assets, materials, and prefabs
  Resources/              Runtime-loaded cat resources
  Scripts/                Gameplay, camera, UI, scoring, audio, and scene logic
  LivingRoom.unity        Main playable scene
Validation/
  Batches/                Ten executable validation batches
  VideoAudit-2026-10-02/  Video audit notes and recent Unity logs
```

## Validation

With Unity closed, run the complete regression suite from the repository root:

```bash
./Validation/Batches/run_all.sh
```

Run one batch with:

```bash
./Validation/Batches/run_batch.sh 04
```

The validation batches cover project configuration, the cat prefab and
animation, movement and jumping, furniture scale and placement, materials,
colliders, camera/bootstrap behavior, menus, audio hooks, Tripo imports, and
full integration. The latest cat-sprite regression completed with
`GAME_VALIDATION_BATCH_20_PASSED`.
