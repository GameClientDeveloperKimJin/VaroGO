# Hidden Path — Stage 2 dungeon blockout

Scene: `Assets/01_Scenes/MapDesign_Stage2.unity` only.

Fantasy Worlds Forest **Ruins / Interior** prefabs supply the carved floors, walls, arch ceilings, pillars, doorways and gates. Torches, statues, crates, chests and rocks are also linked asset instances. Source prefabs and materials are unchanged. New emissive puzzle materials live in this folder.

The drawing's zone lengths add up to more than its stated 300 m footprint. This layout fits approximately 300 × 150 m, with bends and lateral chamber routes providing additional travel distance. Walking elevations progress from 0 to 18 m.

| Zone | Dungeon adaptation |
|---|---|
| Z0 | Roofed entrance nave, statues and ramp to +4 m |
| Z1 | Three 3.8 m lanes; offset red/blue information on glass tiles |
| Z2 | Two alternating lowered/raised crossing decks |
| Z3 | Forge crypt, pushable crates, +2.8 m dais and optional reward |
| Z4 | Role exchange altar, raised ritual hall and CP2 |
| Z5 | Paired red/blue portcullises, alternating open/closed preview |
| Z6 | Three clock-driven chain swings over the abyss |
| Z7 | Three moving hazard rollers and safe side alcoves, CP3 |
| Z8 | Reunion chamber, stairs to +18 m and goal trigger |

CP1 sits between Z2 and Z3. Three optional acorns and moving violet aether-sap patches complete the drawing's nine motif types. Sap is environmental and has no slow/damage effect in this stage.

## Preview

Play the scene directly: WASD/mouse, Space jump, F + movement to push, F1/F2 vision, E at the altar to switch roles, F5 respawn, F6 next checkpoint. Uses the existing offline player, Cinemachine, checkpoints and goal components.

This is a **map skeleton and offline mechanical preview**, not the final networked two-player puzzle implementation. Glass information, alternating bridges, role exchange, mirrored gate timing and crusher movement still need production cooperative rules and online authority integration. A full end-to-end two-player traversal has not been validated.

`Ceilings_HideForEditing` can be hidden in Scene view to inspect the floor plan. The builder's menu items under `Tools / Hidden Path` enforce the Stage 2 scene path. Rebuild replaces this scene's generated layout; manually placed edits should be retained before invoking it.

The editor builder also accepts one-shot local build/render/audit requests in `Temp/Stage2Dungeon.build-request`, used during authoring. No other scene is saved. Generated images and reports are placed under `Captures/Stage2_Dungeon`.
