# Catastrophe

A small Unity prototype about a mischievous tuxedo kitten climbing through a
living room and knocking valuable objects onto the floor.

## Current gameplay

- Explore a furnished living/dining room from a third-person camera.
- Use a pulled-out chair as a step from the floor to the dining table.
- Push the iPad, cup, and MacBook to earn chaos points.
- The first tutorial target places the iPad close to the table edge.
- The cup is staged beside the MacBook as a future spill-hazard setup.
- Furniture, devices, and props remain separate GameObjects and can be moved in
  the Unity Hierarchy.

The scene uses a low-contrast dark wood floor and lighter oak furniture so the
table, climb route, and gameplay objects remain readable from the game camera.

## Controls

| Action | Input |
| --- | --- |
| Move | `WASD` or arrow keys |
| Jump | `Space` |
| Right-paw push | `E` or `F` |
| Touch/mouse movement | Drag |

## Requirements

- Unity `6000.6.3f1`
- Universal Render Pipeline
- Unity Input System

## Run the project

1. Clone the repository.
2. Open the repository root as a Unity project.
3. Open `Assets/LivingRoom.unity`.
4. Press Play.

`Assets/LivingRoom.unity` is also the enabled scene in Build Settings.

## Character and scene details

- Directional Idle, Walk, Jump, and right-paw Push animation states.
- Per-frame paw alignment keeps the visible feet on the physical collider.
- A surface-aware contact shadow follows the floor, chair seat, or tabletop and
  fades while jumping.
- Movement speed is tuned to the authored 8 FPS walk cycle to reduce foot slide.
- Imported GLB furniture is stored as Unity FBX assets with editable scene roots.
- No Apple logos or other decorative brand marks are included.

## Project structure

```text
Assets/
  Art/                    Models, textures, animation sheets, and materials
  Editor/                 Scene builders and validation tools
  Resources/              Runtime cat animation resources
  Scripts/                Gameplay, camera, scoring, and shadow logic
  LivingRoom.unity        Main playable scene
Validation/
  Batches/                Ten executable validation batches
  UnityPreviews/          Character animation preview renders
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

The ten batches cover project configuration, the cat prefab and animation,
movement and jumping, furniture scale and placement, materials, colliders,
camera/bootstrap behavior, and full integration. The latest in-editor run
completed with `GAME_VALIDATION_BATCH_10_PASSED`.
