# Underwater rendering research

## Current implementation

- Unity 6.4 / URP 17.4 stays on the existing renderer configuration.
- The authored scene owns `Deep Ocean Global Volume`, `Course Light`, `School Dive Light`,
  the imported water material, and the imported bubble prefab.
- `VP_DeepOcean` applies Neutral tonemapping, a restrained blue color filter, Bloom, and Vignette.
- Unity fog uses a blue linear 7–112 m range. The short clear range keeps the next opening readable
  while obscuring distant recycled gates.
- No shader, Particle System, renderer, light, or fallback visual is constructed at runtime.

## Verified constraints

- Unity's render-pipeline feature table lists Linear, Exponential, and Exponential Squared fog in URP,
  but local volumetrics and volumetric fog volumes are HDRP-only.
- URP post-processing requires camera post-processing plus a Volume and Volume Profile.
- Therefore true participating-media god rays need a third-party renderer feature in this URP project;
  ordinary spot lights alone do not create visible shafts.

## Free candidates reviewed

### Unity-URP-Volumetric-Light

- Source: https://github.com/CristianQiu/Unity-URP-Volumetric-Light
- License: MIT.
- Explicit Unity 6 RenderGraph support and compatibility through Unity 6000.5.
- Works with Forward, Forward+, Deferred, and Deferred+ and supports main/additional lights.
- Best candidate for a later true god-ray pass. It was not installed now because it adds a renderer
  feature, has transparent-object limitations, and needs a GPU performance pass before committing it.

### URP-WaterShaders

- Source: https://github.com/aniruddhahar/URP-WaterShaders
- License: MIT.
- Provides procedural water surface, triplanar caustics, underwater tint, and distortion for URP 14+.
- Requires Depth Texture and Opaque Texture. It overlaps the already imported Uber Stylized Water,
  so importing it wholesale would duplicate the current water stack.

### Oceana-URP

- Source: https://github.com/ZloyKorovanovich/Oceana-URP
- License: Apache-2.0.
- Includes underwater fog and god rays, but also compute-driven ocean simulation and a deferred
  full-screen setup. It is much broader than this casual corridor game needs.

## Decision

Use authored URP fog, Volume grading, lighting, and the already licensed water/bubble assets now.
Prototype `Unity-URP-Volumetric-Light` on a branch only if real shafts become a visual requirement;
profile transparent fish, bubble particles, and the target GPU before merging the renderer feature.
