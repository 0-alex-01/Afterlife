# Game Design

This document outlines the design specifications for the Portal Room refinement, focusing on layout, mysterious atmosphere, VR compatibility, and nature integration.

## UI Design

- **Color system:** 
    - **Primary:** Deep Obsidian/Near-Black (Surfaces)
    - **Secondary:** Deep Teal/Midnight Blue (Ambient atmosphere)
    - **Accent (Chaos):** Vibrant Purple/Magenta (Portals/Runes)
    - **Accent (Spring):** Bright Emerald/Sky Blue (Portals/Nature)
- **Surface hierarchy:** The floor consists of dark, reflective obsidian tiles with recessed emissive runes. Interactive areas around portals are slightly raised with tonal shifts to signal importance.
- **Interaction Feedback:** 
    - Portals pulsate with emissive intensity when the player approaches within 2m.
    - Particle "tendrils" flow toward the player from the portal center when targeted.

## Asset Design

- **Visual identity:** "Mysterious Fantasy". High contrast between dark, heavy architecture and vibrant, magical emissive elements. Sharp, geometric obsidian structures for the Chaos portal; organic, rounded stone and wood for the Spring portal.
- **Composition rules:** 
    - **Silhouette:** Portals are the primary landmarks. They should stand 2.5m tall, clearly silhouetted against a dark, fog-filled environment.
    - **Scale:** Room radius of 8m to ensure a sense of grandeur while maintaining VR comfort.
    - **Reference:** Match the existing `Portal_Chaos` (purple/black) and `Portal_Spring` (green/blue) visual themes.
- **Nature Patch (core):** 
    - **Location:** Directly under the Spring Portal, extending 1.5m forward.
    - **Style:** Lush, dense grass with small blue and white flower clusters.
    - **Assets:** Utilize `ALP_Assets/GrassFlowersFREE` and `Flowers And Garden Assets`.
    - **Palette:** Deep greens, vibrant teals, and white highlights.

## Game Feedback

- **Genre profile:** Atmospheric (Fantasy/VR)
- **Interaction map:**

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
|------------|------|-----------|--------|------|-----------|--------|-------|-------|-----------|
| Portal Proximity | core | Medium | — | — | Subtle pulse | Emissive ramp | Low hum | — | Signifies interactivity |
| Portal Entry | core | Heavy | Shake (low) | 0.1s hitstop | — | Light flash | Warp blast | Haptic burst | Impactful transition |
| Natural Step | optional | Minor | — | — | Grass sway | Dust puffs | Soft rustle | — | Grounding the player |

## VR & Layout Specifications

- **XR Compatibility:**
    - **Floor Height:** Exactly Y=0.
    - **Player Space:** 2m radius clear zone around the center (0,0,0) for the XR Origin.
    - **Corridors:** No narrow passages (< 3m width).
- **Portal Layout:** 
    - **Arrangement:** Semi-circle at 5m radius from (0,0,0).
    - **Portal 1 (Chaos):** -45° (Front-Left)
    - **Portal 2 (Spring):** 45° (Front-Right)
    - **Portal 3 (Future):** 0° (Center-Front) - Leave a clear, balanced slot for a 3rd portal.
- **Atmosphere Settings:**
    - **Fog:** Deep teal volumetric fog, density 0.05.
    - **Lighting:** Rim lighting on all portals to separate them from the dark background. Emissive floor runes provide low-level fill light.
