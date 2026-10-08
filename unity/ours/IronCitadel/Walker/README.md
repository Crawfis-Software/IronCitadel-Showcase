# Walker: the shared first-person controller (Unity Starter Assets)

Every build (Fable cold, Opus cold and ours) walks with **Unity's Starter Assets first-person controller**,
from the Asset Store package "First Person Third Person Character Controllers". Roger downloaded it on
2026-10-07. It replaces the minimal walker written earlier, which is kept in `../walker-minimal/` as a
fallback.

The package sits in the Asset Store cache:

    %APPDATA%\Unity\Asset Store-5.x\Unity Technologies\3D ModelsCharacters\First Person Third Person Character Controllers.unitypackage

It brings `Assets/Starter Assets/` (first- and third-person controllers, samples). It also declares the
package dependencies `com.unity.cinemachine` 3.1.7 and `com.unity.inputsystem` 1.16.0.

## Install into a project (the cold template has it already)

1. **Gate the project, then back up `Packages/manifest.json`.**
2. **Import the package:** `unity run <project> -- -importPackage "<the .unitypackage above>" -logFile <log>`.
3. **Check the manifest.** `com.unity.cinemachine` must now be listed. `com.unity.inputsystem` must
   still be the project's own version (1.20.0 in the Builder and the template). If the import lowered it
   to 1.16.0, put 1.20.0 back.
4. **Copy `Interact/`** (with any `.meta` files) to `Assets/Interact/`. It holds:
   - `InteractOnE`: the E key;
   - the editor script that makes the player prefab.
5. **Make the player prefab:** run
   `unity run <project> -- -executeMethod IronCitadel.Interact.Editor.IronCitadelPlayerSetup.MakePlayer -logFile <log>`.
   It writes `IronCitadelPlayer.prefab` and `IronCitadelPlayer.result.txt` ("ok ..." or "error ...") next to
   the add-on, here `Assets/Interact/`.
   - The prefab is a variant of Starter Assets' `FirstPersonController/Prefabs/NestedParent_Unpack.prefab`.
   - It holds PlayerCapsule, MainCamera and PlayerFollowCamera (Cinemachine).
   - It has `InteractOnE` on the PlayerCapsule, and the person-sized numbers below.
6. **Place the player.** Drop `IronCitadelPlayer.prefab` into the scene with its root on the start point.
   Remove any other camera tagged MainCamera. The root and the PlayerCapsule's feet are both at the prefab's
   origin, and a Y rotation of 0 faces +z (north).

## Controls (Starter Assets)

| Key | Action |
| --- | --- |
| W A S D | walk at 4 m/s |
| Shift held | sprint at 6 m/s |
| Space | jump |
| Mouse | look |
| E | interact (our add-on, below) |

## The E / Interact contract (secret doors)

E casts a ray 3 m long from the main camera. It skips trigger colliders and the player's own colliders,
and calls the following on the nearest collider it hits:

```csharp
hit.collider.SendMessageUpwards("Interact", SendMessageOptions.DontRequireReceiver);
```

So for a door to open on E, it needs two things:
- a solid (non-trigger) collider;
- a component with a `void Interact()` method on that collider's GameObject or on one of its parents.

## Numbers (a person, set on our variant)

Starter Assets' PlayerCapsule is 1 m wide and 2 m tall (r 0.5, h 2, step 0.25). That clears Synty's 2.05 m
lintels by about 1 cm and fills a 1 m passage, so `MakePlayer` sizes **our variant** to a person. Starter
Assets' own prefabs are not changed.

| Part | Setting |
| --- | --- |
| CharacterController (PlayerCapsule) | radius 0.35, height 1.8, centre y 0.9, step offset 0.3, slope limit 45, skin width 0.02 |
| FirstPersonController | GroundedRadius 0.35 (GroundedOffset -0.14 unchanged) |
| PlayerCameraRoot (the eye) | local y 1.6 |
| Capsule (the visible body and its collider) | scaled to r 0.35, h 1.8 (local scale 0.7, 0.9, 0.7 at y 0.9) |
| The rig | root at the origin, PlayerCapsule at local 0, MainCamera and PlayerFollowCamera at local y 1.6 (Starter Assets parks the root at (22.2, -8.79, 23.9)) |
| PlayerFollowCamera | tracks PlayerCameraRoot; first person: HardLockToTarget + RotateWithFollowTarget, replacing Starter Assets' zero-distance ThirdPersonFollow; vertical field of view 60 (Starter Assets' 40 is narrow indoors) |
| MainCamera | clears to solid black |
| Gravity, jump, speeds | Starter Assets' defaults: -15 m/s², 1.2 m, walk 4 m/s, sprint 6 m/s |

`MakePlayer` writes the prefab next to the add-on: `Assets/Interact/` here and in the cold template,
`Assets/IronCitadel/Walker/` in the Builder. The script finds its own folder, so every copy is the same file.
Re-run it after changing these numbers; `IronCitadelPlayer.result.txt` lists what it set.

**Why the camera is set here.** Starter Assets' `NestedParent_Unpack` still points the follow camera at
PlayerCameraRoot through Cinemachine 2's `m_Follow`. Cinemachine 3 reads `Target.TrackingTarget`, so out of
the box the follow camera has no target and the view stays wherever the camera was parked. `MakePlayer` sets
the target on our variant.

**The conformance kit uses the same numbers:** it bakes and walks with radius 0.35, height 1.8, step 0.3,
slope 45 and skin 0.02 (`Tune` in `kit/conformance/Editor/ConformanceChecks.cs`). Change both together.
