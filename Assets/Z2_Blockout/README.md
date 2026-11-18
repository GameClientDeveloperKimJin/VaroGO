# Z2 mixed rock / hill / mushroom ascent

Scene: Assets/01_Scenes/MapDesign/MapDesign_Stage1_Z0~Z4.unity
Active root: Maps/Z2_MushroomAscent/Z2_Terrace_Exploration

## Current routes

- Main route: three approaches alternate ordinary rock jumps, raised mossy hills, and mushroom crossings. Approach rock heights are +0.85 / +1.70 m above the terrace; each mushroom hill is +2.50 m. The mushroom cap is about +3.225 m above the original ground, so the intended approach climbs the rocks first. Main mushrooms launch at 16 m/s.
- The current scene had three main mushrooms when this revision began. No deleted fourth mushroom was restored. T3 -> T4 now uses a grounded rock ridge with four ascending steps (about 0.85 m increments), finishing on T4.
- Precision branch: T2 -> mushroom -> rock landing -> ordinary jump to mushroom -> rock landing, repeated to T4. Four active mushrooms and four rock landing points replace the former seven floating mushroom-only sequence. The three replaced mushroom objects are retained inactive for reference. Mushrooms launch at 18 m/s; landing rocks are 3.2 m wide. The highest active cap is approximately 25.925 m world height.
- All active mushrooms have ground at their bases. Airborne branch objects now stand on continuous cliff columns reaching below the water surface (base Y=-4). The hidden shortcut alcove also has bedrock below its deck. Twenty attached rock masses broaden the column bases.
- New meshes use exact static MeshColliders instead of the default cylinder CapsuleCollider, so shallow rock steps have flat, correctly sized collision surfaces.
- Seven decorative clusters that obstructed the new approaches/exit were disabled. The remaining dense forest, false choices, checkpoints, water and fall-reset systems remain in place.
- Fourteen false mushrooms retain their sideways/backward 24–30 m/s launch and 20–22 m/s upward speed. Cap spot count does not identify the answer.

## Authored groups

08_Mixed_Rock_Hill_Ascent contains the rock steps, hills, supported cliff branch and Route_Test_Nodes (empty editor reference transforms).
07_Precision_Mushroom_Chain retains the active mushroom portion and the inactive replaced mushrooms.
The earlier layouts remain archived inactive; do not enable them together.

## Verification

Captures/Z2/MixedAscent contains a pre-edit scene copy, scene hashes, edge tests, continuous tests and an overview image.

- All 25 authored transitions passed actual PlayerController / CharacterController collision and movement tests at 60 Hz.
- Five continuous sections (three main mixed approaches/crossings, the T3 -> T4 rock ridge, and the entire mixed precision branch) passed at both 30 and 60 Hz without teleporting between steps.
- Downward support queries passed for every active Z2 JumpMushroom base.
- Other scene files were checked against pre-edit hashes.
- These are scripted movement tests, not human difficulty evaluation or two-client network tests.

The existing Z2ShortcutMushroom remains an offline preview flight to Z4. It was not modified in this revision. Network shortcut movement still requires authority integration and two-client validation. Shared player scripts and source prefabs were not changed in this revision.

Earlier revisions and their historical test logs remain under Captures/Z2/ForestRefinement and Captures/Z2.
