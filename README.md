# Shaders

A personal Unity playground for learning how to write shaders. Each effect is built twice: first visually in **Shader Graph**, then by hand in **HLSL** — to understand what the nodes actually compile down to.

- **Unity:** 6000.3.9f1 (Unity 6.3)
- **Render pipeline:** URP 17.3 (PC and Mobile pipeline assets in `Assets/Settings`)
- **Demo scene:** `Assets/Scenes/SampleScene.unity`

## Project layout

```
Assets/
├── Scenes/SampleScene.unity           # test scene with primitives and ProBuilder meshes
├── Scripts/Editor/
│   └── NoiseTextureGenerator.cs       # editor tool for baking noise textures
├── Shaders/
│   ├── Graphs/                        # Shader Graph versions
│   │   ├── Dissolve_Shader_Graph.shadergraph
│   │   └── Shader Graphs_Dissolve_m.mat
│   └── HSLS/                          # hand-written HLSL versions
│       ├── Dissolve.shader
│       └── Custom_Dissolve_m.mat
└── Textures/
    ├── Noise.png                      # generated noise textures
    └── Noise2.png
```

## Effects

### Dissolve

An object "burns away" based on a noise pattern, with a glowing band along the dissolving edge.

**How it works:**

1. Sample a noise value (0–1) for every pixel.
2. Discard the pixel if `noise < _Dissolve` — raising `_Dissolve` from 0 to 1 eats the object away.
3. Pixels whose noise is just above the threshold (`noise <= _Dissolve + _EdgeWidth`) get the edge color instead of the base color.
4. The noise UVs scroll over time (`_Time.y * _AnimationSpeed`), so the edge keeps moving.

#### Shader Graph version — `Shaders/Graphs/Dissolve_Shader_Graph.shadergraph`

- URP **Lit** target, opaque with **Alpha Clipping** enabled.
- Noise is procedural (**Gradient Noise** node), fed by **Time → Multiply → Tiling And Offset** for scrolling.
- The noise drives **Alpha** against the **Dissolve** threshold; a **Step** node builds the edge mask, which is tinted with **Edge Color**.

| Property        | Reference          | Description                         |
|-----------------|--------------------|-------------------------------------|
| Color           | `_Color`           | Base color                          |
| Noise           | `_Noise`           | Gradient Noise scale (default 10)   |
| Dissolve        | `_Dissolve`        | Dissolve amount (0 = whole, 1 = gone) |
| Edge Width      | `_Edge_Width`      | Width of the glowing edge band      |
| Edge Color      | `_Edge_Color`      | Edge color                          |
| Animation Speed | `_Animation_Speed` | Noise scroll speed                  |

#### HLSL version — `Shaders/HSLS/Dissolve.shader` (`Custom/Dissolve`)

A hand-written URP shader that reproduces the graph's logic. It is **unlit** (outputs a flat color) and uses a noise **texture** instead of procedural noise.

Things worth noting in the code:

- **SRP Batcher compatibility** — all material properties live in a `CBUFFER_START(UnityPerMaterial)` block.
- **Shared code in `HLSLINCLUDE`** — the `SampleDissolveNoise()` function (scroll + `clip()` + `step()`) is shared by both passes, so the shadow matches the visible shape.
- **Custom `ShadowCaster` pass** — without it, a dissolved object would still cast the shadow of the full mesh. The pass:
  - applies `ApplyShadowBias()` to avoid shadow acne,
  - handles both directional (`_LightDirection`) and punctual lights (`_CASTING_PUNCTUAL_LIGHT_SHADOW` / `_LightPosition`),
  - clamps depth to the near plane, respecting `UNITY_REVERSED_Z`.
- `RenderType = TransparentCutout`, `Queue = AlphaTest` — the correct setup for clip-based (cutout) rendering.

| Property        | Name              | Description                          |
|-----------------|-------------------|--------------------------------------|
| Base Color      | `_BaseColor`      | Base color                           |
| Noise           | `_NoiseTex`       | Noise texture (red channel is used; supports tiling/offset) |
| Dissolve        | `_Dissolve`       | Dissolve amount, 0–1                 |
| Edge Width      | `_EdgeWidth`      | Width of the edge band, 0–1          |
| Animation Speed | `_AnimationSpeed` | Noise scroll speed, 0–2              |
| Edge Color      | `_EdgeColor`      | HDR edge color (pairs well with Bloom) |

## Tools

### Noise Texture Generator

**Menu:** `Tools → Open Noise Generator`

An editor window that bakes tileable grayscale noise into a PNG — used to make the textures for the HLSL dissolve shader.

- **Algorithm:** fBm (fractal Brownian motion) — several octaves of `Mathf.PerlinNoise`, each with double the frequency and half the amplitude, normalized back to 0–1.
- **Settings:** size (128–2048), scale (number of noise "cells" across the texture), octaves (1–8), seed (with a *Random* button).
- **Live preview** at 128×128. Sampling is done in normalized UVs, so the preview shows the same pattern as the final texture at any resolution.
- **Import settings** are applied automatically: **sRGB off** (noise is data, not color) and **Wrap Mode: Repeat** (so it can scroll).
- Asks before overwriting an existing file; the output folder must be inside `Assets`.

> Note: Perlin noise isn't truly periodic, so the texture may show a faint seam when it wraps.

## Getting started

1. Open the project in Unity 6000.3.9f1 (or newer 6.3).
2. Open `Assets/Scenes/SampleScene.unity`.
3. Select an object using one of the dissolve materials and drag the **Dissolve** slider.
4. Optionally generate a new noise texture via `Tools → Open Noise Generator` and assign it to `Custom_Dissolve_m`.

## Learning notes

| Shader Graph                      | HLSL equivalent                                   |
|-----------------------------------|---------------------------------------------------|
| Alpha + Alpha Clip Threshold      | `clip(noise - _Dissolve)`                         |
| Step node                         | `step(edge, x)`                                   |
| Time node                         | `_Time.y`                                         |
| Tiling And Offset                 | `TRANSFORM_TEX` + adding a UV offset              |
| Sample Texture 2D                 | `SAMPLE_TEXTURE2D(tex, sampler, uv)`              |
| Lerp                              | `lerp(a, b, t)`                                   |
| "Cast Shadows" checkbox           | A whole `ShadowCaster` pass written by hand       |
