# Game Design — MirrorRoom (Afterlife VR Installation)

**Domains covered:** Asset Design (room mood + 5-stage soul reflection art direction), Game Feedback (proximity / dwell / touch communicated through light, particles, motion, audio). UI Design is intentionally minimal — this is a diegetic VR meditation piece with **no HUD**; the world and the reflection carry all state.

**Render pipeline:** URP. **Platform:** VR (AutoHand + XR Interaction Toolkit; ghost hands provided by the existing `GhostlyHand` asset). All material/lighting language assumes URP capabilities (emissive + Bloom volume, fresnel rim, fog).

**Continuity with existing Afterlife look (do not break):** translucent body with a **cyan fresnel rim** (RGB 0.25, 0.69, 0.80 = `#40B0CC`) as the soul's signature; cave-like reverberant ambience; memory-fragment minimalism; smooth `SmoothStep` state crossfades (~2.5s baseline); `EaseOutBack` spawn-in; gentle idle bob (~0.12 m amplitude, ~7.8 s period, ~12°/s spin). The five soul states below are the meditation-room analog of the project's existing world-state system (Harmonie / Zerfall / Chaos) — extended, not replaced.

**VR safety rules that override any effect below:** keep the room near-black with gentle contrast; no rapid **full-screen** flashing; the Chaos flicker stays **localized to the reflection and the beam**, never the whole view; transitions stay slow and eased to protect comfort; only the mirror, the beam, and the player's own glow emit meaningful light.

---

## Room Mood

A dark, near-black, minimal meditation chamber — a held breath. One large standing mirror is the sole focal point, lit by a single focused beam from directly above. Everything else recedes into shadow so the player's gaze has nowhere to go but the reflection.

- **Base environment:** matte near-black surfaces, `#050506` walls / `#060709` floor. No texture noise that reads as "material" — the room should feel like volume, not architecture. Subtle, slowly drifting volumetric fog catches the beam.
- **Floor:** very faint mirror-like sheen directly under the standing mirror only — a soft pooled reflection of the beam and the soul's glow shimmering on the ground, falling off to black within ~2 m. This is the *only* secondary light bounce.
- **Light budget:** three emissive sources total — (1) the overhead beam, (2) the mirror reflection itself, (3) the player's soul glow + ghost hands. Nothing else lights the room. Ambient is near-zero so the soul reads as the brightest thing the player carries with them.
- **Scale:** mirror ~2.4 m tall × ~1.2 m wide, standing on the floor, top edge well above eye height — large enough to feel like a doorway, intimate enough to approach. Beam cone ~1.4 m diameter at the mirror, narrowing toward the ceiling.
- **Comfort tone:** low, quiet, slow. Drift and shimmer are the only constant motion; nothing in the periphery jerks or strobes.

---

## Soul States Palette

Build everything from the Clear Soul cyan baseline. Each state shifts the soul-body glow, the overhead beam, the fog tint, and the particle color **together** so the whole room reads as one emotional key. Room base stays near-black in every state — only tint and intensity move.

| State | Mood | Soul body glow (fresnel rim) | Overhead beam | Fog tint | Particle color | Intensity feel |
|-------|------|------------------------------|---------------|----------|----------------|----------------|
| **1. Clear Soul** | calm, neutral, ethereal (default) | `#40B0CC` (0.25, 0.69, 0.80) | cool cyan-white `#BFEAF2`, soft & steady | near-black cyan `#070C0F` | faint cyan motes `#6FC8DC` | low, even, restful |
| **2. Bloom / Summer** | warm, alive, hopeful | warm gold `#E8B65A` (0.91, 0.71, 0.35) | amber-gold `#F2C87A`, full & gently breathing | warm dark `#0E0A06` | soft pollen gold `#F4D98C` | brightest of the five, but warm not harsh |
| **3. Decay** | melancholic, fading | cold blue-grey `#7E8C99` (0.49, 0.55, 0.60) | pale desaturated grey-blue `#9DA8B2`, dimmed & thin | cold grey-black `#080A0C` | ashen blue-grey `#8A95A0` | lowest, drained, light feels "used up" |
| **4. Chaos** | unsettling, fractured | magenta `#C84B9E` (0.78, 0.29, 0.62) | magenta `#D24FB0`, **flickering** (localized) | dark magenta-black `#0C060A` | magenta shards `#E05CC0` + cyan-offset doubles `#40B0CC` | unstable, jittering between dim and mid |
| **5. Light Form** | transcendence, ascension | near-pure warm white `#F7F4EC` (0.97, 0.96, 0.93) | pure white `#FFFFFF`, clean & expansive | faintly lifted warm black `#0C0C0E` | bright rising white motes `#FFFDF5` | high but soft-bloomed, weightless, never clinical |

**Palette discipline:** cyan (`#40B0CC`) is the home key — every transition departs from and can return to it. Magenta is *reserved exclusively for Chaos* (the only "alarm" hue in the room); it must never leak into the other four states' particles or beam. Gold and white are the only "rising" warm tones; blue-grey is the only "falling" cold tone.

---

## Light Beam

One focused volumetric cone from above, the room's heartbeat. It never moves position — only color, intensity, and texture change with state. Bloom (URP volume) gives it soft halation without raising overall scene brightness.

- **Clear Soul:** steady cool cyan-white, mid-low intensity, a calm constant column. Faint, slow volumetric drift inside the cone (~one gentle pulse every ~6 s, ±8% intensity) so it breathes rather than sits dead.
- **Bloom:** warms to amber-gold and swells to the room's brightest, fullest cone; a slow, generous "breathing" pulse (~4 s cycle, ±12%) like warmth radiating.
- **Decay:** desaturates to pale grey-blue and **dims** — the cone thins, edges soften and the volumetric fog inside it grows sparse, as if the light is being forgotten. Occasional slow, gentle fade-dip (~±15% over ~3 s), never a snap.
- **Chaos:** magenta, **flickering** — irregular rapid intensity stutter and a slight lateral jitter of the cone, **confined to the beam and its fog volume only**. Flicker amplitude scales with proximity but is capped so it never approaches full-screen brightness change; comfort first. Think "failing fluorescent seen in peripheral vision," not strobe.
- **Light Form:** resolves to clean pure white, the cone widening softly and bleeding upward — the ceiling-end dissolves into bloom as if the beam and the room are opening. Steady, expansive, no flicker; a single slow brightening swell on entry then held.

**Transitions:** beam color/intensity crossfade on the same `SmoothStep` ~2.5–3.5 s curve as the soul body, so light and reflection always change as one.

---

## Particles

All particle systems are low-count and slow — this is meditation, not spectacle. Counts are qualitative density targets; favor *fewer, softer, longer-lived* over busy. Particles are additive-glow, tinted per the palette table, and concentrate **around the reflection inside the mirror and in the beam cone**, not across the whole room.

- **Clear Soul — ambient motes:** very sparse, tiny cyan points drifting almost weightlessly upward and sideways with faint random wander. Long lifetime, soft fade in/out. Presence, not motion. *Density: minimal.*
- **Bloom — pollen:** soft, slightly larger warm-gold flecks drifting and gently rising, lazy swirling drift (low turbulence), catching the beam like dust in sun. Warm, alive, unhurried. *Density: medium — the most populated state, but still calm.*
- **Decay — dissolving fragments:** small ashen blue-grey flecks that **shed off the reflection** and float away/downward, fading as they fall — the form is coming apart. Reuse the project's crumble-to-dust character (sink + dissolve). Sparse and intermittent, emphasizing emptiness. *Density: low, fading.*
- **Chaos — glitch shards:** small angular magenta shards plus occasional cyan-offset duplicates that **stutter and jitter** in place near the reflection, popping in and out with short erratic lifetimes — visual noise tied to the doubled/offset reflection. Localized tight to the mirror, never drifting into the room. *Density: bursty, irregular — feels like more than it is.*
- **Light Form — rising motes:** bright white motes streaming gently and continuously **upward** from the soul, dissolving into bloom near the top of the beam — ascension made visible. Soft, long-lived, serene. *Density: medium, steady upward flow.*

---

## Motion & Morph Feel

The reflection is *the soul*, not a mirror — it follows the player but never perfectly. Everything is eased; nothing snaps.

- **Follow lag:** the reflection tracks the player's head/hand motion with a soft **~0.15–0.25 s delay** (critically damped, no overshoot wobble) so it feels like it's catching up from somewhere deeper than glass. The lag is constant and gentle — readable as "alive," never as input latency on the player's own body (only the *reflection* lags, the hands themselves track 1:1 for comfort).
- **Hover rhythm:** the soul (and its reflection) idles on the established Afterlife bob — ~0.12 m amplitude, ~7.8 s period, ~12°/s slow drift-spin — so even when the player holds still the reflection breathes. The reflection's bob runs slightly out of phase with the player's, reinforcing the "separate being" feeling.
- **Close-range split:** as the player comes very near (< ~1 m, see Feedback), the single reflection briefly **splits into 2–3 overlapping, semi-transparent versions** showing adjacent stages side by side, each at reduced opacity and slightly offset with its own micro-lag. They drift apart a few centimeters and re-converge — a glimpse of the soul's other selves. This recombines smoothly when the player backs away.
- **Morph transition curve:** state-to-state morphs ride a slow ease-in-out (`SmoothStep`, ~2.5–3.5 s, longer than the project's 2.5 s default to feel dreamlike). Body glow color, beam, fog, and particle emission all crossfade on the same curve. The newly arriving form scale-settles with a soft overshoot (`EaseOutBack`, peak at ~30% of the transition, ~10–15% overshoot) so it "blooms" into being rather than blinking on.
- **No hard cuts, ever:** even Chaos arrives by easing in; its instability lives in the *flicker layer on top of* a smoothly-blended base, not in the transition itself.

---

## Feedback Design

**Genre profile:** Atmospheric (meditative VR installation) with a light Tactile/Confirmation layer on the touch interaction. Restraint is the design — feedback is built from *intensification of existing ambient elements* (beam, glow, particles, reverb) rather than added spectacle. Three input languages drive the morph: **proximity**, **dwell**, and **touch**.

### (a) Proximity feedback — distance intensifies the morph
The closer the player floats to the mirror, the more present the current state becomes. Map distance to a 0→1 intensity that drives glow rim strength, beam intensity/flicker, and particle density together:

- **Far (> ~3 m):** subtle — soul glow low, beam calm, particles minimal. The state is legible but quiet.
- **Mid (~1–3 m):** intensity ramps smoothly (eased, not linear) — rim brightens, particles thicken, beam breathes more.
- **Near (< ~1 m):** peak intensity + the **close-range split** (see Motion) engages; the reflection feels most "awake" and otherworldly here. Chaos flicker is at its strongest here but still capped for comfort.

Intensity follows the player smoothly with the same soft damping as the reflection — approaching feels like leaning into the soul, backing away like releasing it.

### (b) Dwell feedback — stillness cycles the states
When the player holds roughly still and faces the mirror for a sustained moment, the reflection begins to **slowly auto-cycle** through the five states on its own — meditation rewarded with transformation.

- **Trigger:** continuous gaze-at-mirror + low movement for ~3–4 s before the first auto-advance.
- **Cadence:** advance one state roughly every ~12–18 s while dwell continues, each on the slow morph curve — unhurried, hypnotic.
- **Release:** any significant movement gently pauses the auto-cycle (no jarring stop); it resumes after the dwell condition is re-met. A barely-perceptible swell in the beam's breathing signals "the room is listening" while dwelling.

### (c) Touch feedback — ghost hands advance the state
When a ghost hand contacts the mirror surface, it's the one moment of clear, satisfying confirmation — and it **advances to the next state**. Layer three channels, restrained and brief:

| Layer | Effect | Timing |
|-------|--------|--------|
| Visual — ripple | A soft concentric light ripple expands outward across the mirror surface from the contact point, fading as it grows | begins at 0 ms, expands over ~0.8–1.2 s, fades by end |
| Visual — light pulse | The beam and the soul rim give a single gentle brightness pulse (overshoot then settle, peak ~+15% at ~30% of duration) | peak ~250 ms, settled by ~700 ms |
| Audio — touch cue | A soft resonant chime/bell tail, spatialized at the contact point, with cave reverb; ±10–15% pitch variation so repeated touches never sound identical | onset at 0 ms, tail decays into the new state's ambience |

The state morph itself begins right after the touch pulse so the ripple *causes* the transformation — anticipation (hand approaches, rim brightens slightly) → action (contact: ripple + pulse + chime) → reaction (the soul morphs to the next state on the slow curve).

### Interaction map

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
|-------------|------|-----------|--------|------|-----------|--------|-------|--------|-----------|
| Approach mirror (proximity) | core | Light | — | — | reflection rim/particle ramp, eased w/ distance | glow + beam + particle density rise | ambient bed swells slightly | continuous distance read | Core loop — rewards moving in, the primary way the room responds |
| Close-range (< 1 m) | core | Medium | — | — | reflection splits into 2–3 lagged overlays | peak glow, peak particles | ambient fullest | distance threshold | Signature "soul's other selves" moment; defines the piece |
| Dwell / stillness cycle | core | Light | — | — | slow auto-morph through states | beam "listening" swell | per-state ambience crossfades | gaze + low-movement timer | Meditation reward; lets passive players experience all 5 states |
| Touch mirror (advance state) | core | Medium | — | — | rim/beam pulse (overshoot+settle) | expanding surface ripple | spatial chime, ±10–15% pitch, reverb tail | hand-surface contact | The one tactile confirmation; player agency over transformation |
| State morph transition | core | Medium | — | — | EaseOutBack settle of new form | glow/beam/fog/particle crossfade (SmoothStep ~3 s) | ambient crossfade between state beds | — | Must feel dreamlike, never jarring — comfort + tone |
| Chaos instability | optional | Medium | — | — | doubled/offset reflection jitter | **localized** magenta beam flicker + glitch shards | dissonant/glitch audio layer | active in Chaos state | Adds unease; gated optional because it carries the strictest VR-comfort constraint |
| Floor reflection shimmer | optional | Minor | — | — | — | soft pooled glow under mirror, tinted to state | — | — | Ambient richness; cut first if performance/comfort requires |

### Sequences

- **Touch → advance:** hand nears mirror, rim brightens subtly (anticipation, ~last 150 ms) → contact: ripple spawns + beam/rim pulse + spatial chime (0 ms) → pulse peaks (~250 ms) → state morph begins (~300 ms) → ripple fades (~1 s) → new state fully settled + its ambience faded in (~3–3.5 s).
- **Dwell → cycle:** stillness held (~3–4 s) → beam "listening" swell (subtle) → auto-advance morph (~3 s) → hold in new state (~12–18 s) → repeat until movement.

---

## Audio Design

Spatial, cave-reverberant ambient beds — one per state — that crossfade as the soul morphs, consistent with the existing Afterlife drone-that-scales-with-state approach. Audio is the room's emotional carrier when the visuals stay restrained; volume and presence ramp gently with proximity (closer = fuller, more enveloping), mirroring the visual intensity curve.

- **Clear Soul:** ethereal, weightless — a soft sustained cyan-cool pad with long cave reverb tails and sparse, distant shimmer tones. Spacious and still.
- **Bloom / Summer:** warm and alive — the pad warms and fills out, gentle organic overtones (faint warmth like distant birdsong abstracted into tone), a subtle rising harmonic. The most "present" and comforting bed.
- **Decay:** hollow and melancholic — the pad thins and lowers, harmonics drop out, longer silences between sparse tones, more cavernous emptiness. Light feels far away; sound feels like memory.
- **Chaos:** dissonant and glitchy — detuned/beating layers, brief stuttered grains and clipped artifacts synced loosely to the visual flicker, **kept low in level and spatialized to the mirror** so it unsettles without assaulting (no sharp transients near the ears — VR comfort). Tension, not jump-scare.
- **Light Form:** resolved and harmonic — dissonance clears into a clean, consonant chord that slowly opens upward, bright but soft-edged, reverb blooming. Release and ascension.

**Transitions / crossfade:** state ambience beds crossfade on the **same ~2.5–3.5 s `SmoothStep` curve** as the visual morph, so sound and image arrive together — never a hard audio cut. The touch chime (above) rings *over* the crossfade and resolves into the incoming bed. All beds share the same cave reverb space for continuity, and every repeating discrete sound (chime, shimmer hits) carries ±10–15% pitch variation to avoid mechanical repetition.

---

## Assets Needed

Per asset: tier, type, style reference (match), palette, usage, approximate in-scene size.

- **Soul reflection material/form (core)** — translucent fresnel-rim body, the project's signature look. *Match:* existing soul body — translucent with cyan fresnel rim `#40B0CC`. *Palette:* per-state glow from the table. *Usage:* the morphing reflection inside the mirror. *Size:* full-body, ~1.6 m tall floating form, no legs.
- **Ghost hands (core)** — already provided by `GhostlyHand` (URP). *Match:* its existing ethereal translucent look, re-tinted toward soul cyan for continuity. *Usage:* player's hands + the touch interactor. *Size:* hand-scale, 1:1 with VR controllers.
- **Standing mirror (core)** — large frameless or thin-framed standing mirror surface that renders the reflection + ripple. *Match:* minimal, near-black surround so the frame disappears into the room. *Palette:* base `#050506`. *Usage:* sole focal point. *Size:* ~2.4 m × 1.2 m.
- **Overhead light beam / volumetric cone (core)** — single focused beam with volumetric fog in-cone. *Palette:* per-state beam colors. *Usage:* room key light + flicker carrier. *Size:* ~1.4 m diameter at mirror.
- **Volumetric room fog (core)** — slow-drifting low fog tinted per state. *Palette:* fog tints from table, all near-black. *Usage:* atmosphere + beam catch. *Size:* room-filling, low to floor.
- **Particle systems ×5 (core: Clear, Bloom, Decay; optional: Chaos shards, Light Form motes can ship after)** — additive-glow, per-state character described above. *Match:* reuse project crumble-to-dust feel for Decay, shatter-shard feel for Chaos. *Palette:* particle colors from table. *Usage:* around reflection + beam. *Size:* small flecks, sub-cm to few-cm.
- **Touch ripple effect (core)** — expanding concentric ring on the mirror surface. *Palette:* current state glow color. *Usage:* touch confirmation. *Size:* expands from point to ~mirror width.
- **Floor reflection shimmer (optional)** — soft pooled glow under the mirror. *Palette:* tinted to current state. *Size:* ~2 m radius falloff.
- **Audio beds ×5 + touch chime (core: beds + chime; optional: Chaos glitch layer)** — spatial cave-reverb ambient loops per state plus a pitch-varied touch chime. *Match:* existing cave-like reverberant Afterlife ambience. *Usage:* per-state mood + touch cue, crossfaded on the morph curve.

---

## Choice Mechanic (Three Pedestals)

A second movement of the same piece: after the soul has reflected, the room offers a **choice**. Three low pedestals stand in a shallow **semicircle facing the mirror** (~2.5–3 m back from it, ~1.3 m apart, arc centered on the player's approach line). Each pedestal is the same matte near-black as the room (`#050506`) so only its floating object reads — a single object hovering ~0.30 m above each plinth (~0.9 m tall), slowly bobbing and rotating to draw the eye. This is *not* a menu: it is three quiet invitations the soul drifts between. Everything here obeys the same home-key discipline, slow `SmoothStep` crossfades, Bloom + fresnel-rim language, and VR-comfort rules as the rest of the room — **no full-screen flash, no strobe, all eased.**

**Attract motion (all three objects):** the established Afterlife idle, scaled down for the small props — bob ~0.04–0.06 m amplitude, ~7.8 s period, ~12°/s slow spin. Out of phase with each other so the trio never pulses in unison. Each sits in its own faint, tightly-falling-off pool of its theme glow on the pedestal top (≤ ~0.4 m radius) — local light only, the room stays near-black between them.

### The Three Objects — art direction & palette

- **VOID — the extinguishing candle / dark sphere.** A near-black matte sphere (or a guttering candle whose flame is almost out) that *consumes* light rather than emits it. Emotional key: **absence, the dying ember.** Body `#0A0E12`; an almost-spent ember core that pulses dimly **darker→out** — `#1E3A4A` drained-cyan low-glow fading toward black `#080C10` on a slow ~5 s breath (never brightening past low). A barely-there cold rim `#2E5A6E` (the home cyan, starved of life). Faint thin smoke wisp drifting up and dissolving — the *only* particle, sparse, ashen `#243842`. It should feel like a hole in the room's glow, not a lamp.
- **REBIRTH — the sprouting seed / egg.** A smooth pale egg or seed with a single green sprout, warm and **alive**. Emotional key: **growth, hope.** Body soft off-white shell `#E8E4D8`; living-green emissive glow `#4FC57A` with a gentle ~3.5 s breathing swell (±12%, the warmest/fullest of the three but soft, never harsh). Soft drifting **motes rising** around it, green `#86E2A0` with a faint warm-gold tip `#CFE39A` — relate to Bloom's pollen but **green, not gold.** This is the one comforting object; its glow leans slightly fuller as the soul nears.
- **JUDGEMENT — the split crystal / scale.** A vertical faceted crystal (or a hanging balance-scale) cleanly divided down the middle. Emotional key: **tension, balance held.** One half **mercy/light** — warm gold `#F2C24E` (continuous with the room's Bloom gold family) bleeding to near-white `#F7F4EC` at the edge; the other half **wrath/dark** — deep red `#C8412E` over a dark body `#160A08`. The two halves breathe in slow **opposition** (~4 s: as gold swells, red dims, and back) so it visibly *teeters*. A thin charged seam of light runs the divide. Sparse particles: gold motes `#F4D98C` rising on the light side, dim red embers `#9E3322` sinking on the dark side — never crossing the seam.

> **Palette discipline (extends the Soul States rule):** **green** (`#4FC57A`) is the new *living* key, reserved exclusively for Rebirth; **red** (`#C8412E`) is reserved exclusively for Judgement-wrath. Neither leaks into other states, and **magenta stays Chaos-only.** Gold reuses the existing Bloom warm family for continuity; Void's glow is the home cyan deliberately drained toward black. The room base stays near-black under all three — only local object/mirror/portal tint moves.

### Hover Preview — the mirror *leans*, non-committally

As a ghost hand approaches an object (within ~reach, ~0.6 m of it), the mirror begins to **lean toward that theme without committing** — a reversible "tasting" of the choice:

- The mirror's current reflection **partially blends** toward the target mirror-state — only ~30–40% of the way — on a gentle but responsive **~0.5 s `SmoothStep`** in. Beam and fog tint drift the same partial amount. The reflection stays clearly itself, just *tinted with possibility*.
- The approached object brightens subtly (+~15%) and its bob/spin quickens a touch — "this one is listening." The other two stay neutral.
- **Withdraw and it reverts:** if the hand pulls back without grabbing, everything eases home to neutral over **~1.0–1.2 s** (slightly slower out than in, so it feels like a held breath releasing). **No commitment cue yet** — no ripple, no chime, no portal hint. Hovering is consequence-free and fully reversible.
- Comfort: the partial blend never approaches full brightness or full-view tint; it's a localized lean in the mirror + beam only.

### Commit — the grab transforms everything

Grabbing the object is the one decisive act. Anticipation (hover lean already present) → **action** (grasp) → **reaction** (full transform), all under the slow-eased comfort rules:

1. **Confirm beat (0 ms):** the grabbed object gives one soft inward pulse and dissolves *into the hand / toward the mirror* (it's spent, not pocketed); a single spatial confirm chime rings (cave reverb, ±10–15% pitch). No flash.
2. **Mirror transforms (begins ~150 ms):** the partial hover-lean completes the rest of the way to the **full** mirror-state on the canonical **~3 s `SmoothStep`** (beam, fog, particles crossfade together, as always). New mirror form scale-settles with the usual `EaseOutBack` (peak ~30%, ~10–15% overshoot).
3. **The other two recede:** the unchosen pedestals' object glows **dim toward black and fade in opacity** over ~1.5–2 s, bob slowing to rest — the choice closes the alternatives quietly, no harsh snap-off. They settle to dark, present-but-dormant silhouettes.
4. **Portal is born (begins ~600 ms, ~2.5–3 s to full):** a doorway opens in front of / beside the mirror, growing **from a seed point** with `EaseOutBack` scale-settle and a slow Bloom swell (brightness rises by halation, **never a flash**). It reaches a walk-through size (~2.2 m tall × ~1.1 m wide, a soul-doorway matched to the mirror's scale).
5. The room's ambient bed crossfades to the chosen theme on the same ~3 s curve; a low, slow rumble/haptic may underlie heavy themes (Void, Judgement) — low-frequency, no sharp transient.

### The Three Mirror States

Each is a full member of the morph system (same crossfade curve, same Bloom/fresnel language):

- **VOID — emptiness.** The mirror goes **fully black**; the reflection **dissolves and disappears** entirely. Reuse the Decay shed-to-dust character but carried all the way to *nothing* — the soul-form sheds ashen `#243842` flecks that fall and fade, the beam over the mirror dims to near-out, and the glass settles to flat `#050506` void. The one state where the mirror stops reflecting at all — a held, empty stillness.
- **REBIRTH — bloom of life.** The mirror **blooms green** — structurally the Bloom/Summer state **recolored green, not gold.** Living-green glow `#4FC57A` fills the reflection, green pollen motes `#86E2A0` drift and rise (the most populated, warmest of the three), beam warms to a soft green-white `#CFF0D8`, fog tints `#070C09`. Full, breathing, hopeful — but never harsh.
- **JUDGEMENT — division.** The mirror **splits into two vertical halves** with a charged seam down the center: one half **bright/light** (gold `#F2C24E` → white `#F7F4EC`), one half **dark/red** (`#C8412E` over `#160A08`). The reflection appears differently in each half (illuminated vs. shadowed) and the two halves breathe in slow opposition, the seam pulsing faintly. Tension and balance made literal.

### The Three Portals — art direction

Each portal is a **doorway the soul walks through**; on passing through, the view performs a slow, eased **fade-to-theme-color** (~1.5–2 s `SmoothStep` comfort fade — the one allowed full-view change, a gentle transition fade, *not* a flash) before the corresponding Level 3 scene loads.

- **VOID portal — the rift.** A **dark tear into blackness** — a vertical jagged rift with faint, cold edges (`#2E5A6E` drained-cyan rim haze, very dim) framing an interior of pure `#000000`. No particles spilling out; if anything, faint cold motes are *drawn in*. Quiet, ominous, still. **Walk-through fades to black** (`#050506`).
- **REBIRTH portal — the living ring.** An **organic pulsing ring of green-gold light** — a soft, breathing oval of `#4FC57A` core with a warm-gold `#CFE39A` outer halo, edges alive with slow drift and rising green motes, a gentle ~3 s pulse. Soft, warm, welcoming. **Walk-through fades to green** (`#1A4A2C` deep living green).
- **JUDGEMENT portal — the split door.** A **vertical split portal**: **golden top half** (`#F2C24E` → white bloom) and **glowing red bottom half** (`#C8412E`), divided by a bright charged horizontal seam. The halves breathe in opposition like the mirror. Imposing, weighty. **Walk-through fades to a balanced gold** (`#6E561E` warm) crossed with red at the edges.

### Choice Palette & Timing Summary

| Theme | Object glow | Mirror state | Portal | Particle color | Transition feel |
|-------|-------------|--------------|--------|----------------|-----------------|
| **VOID** | dark-blue/ember fading to black `#1E3A4A`→`#080C10`, rim `#2E5A6E` | mirror fully black, reflection dissolves & vanishes (`#050506`) | dark rift, cold faint edges `#2E5A6E`, interior `#000000` | ashen `#243842`, sparse, drawn inward | slowest, draining — Decay-shed carried to nothing; fade to black |
| **REBIRTH** | living green `#4FC57A`, breathing ±12% | green bloom + rising motes (Bloom recolored green) | pulsing green-gold ring `#4FC57A`/`#CFE39A`, ~3 s pulse | green `#86E2A0` + gold tip `#CFE39A`, rising | warmest, fullest, alive; fade to deep green |
| **JUDGEMENT** | split gold `#F2C24E`/white `#F7F4EC` vs red `#C8412E`, opposed breath | vertical split — light half + dark/red half, seam pulse | vertical split door, gold top / red bottom, charged seam | gold `#F4D98C` rising (light) / red `#9E3322` sinking (dark) | tense, teetering, balanced; fade to balanced gold |

### Interaction map (choice mechanic)

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
|-------------|------|-----------|--------|------|-----------|--------|--------|-------|-----------|
| Approach object (hover preview) | core | Light | — | — | mirror partial-leans 30–40% toward theme (~0.5 s in) | object +15% glow, faster bob; beam/fog partial tint | ambient leans subtly toward theme bed | hand within ~0.6 m of object | Lets the soul *taste* each choice reversibly before committing |
| Withdraw hand (revert) | core | Light | — | — | mirror eases back to neutral (~1.0–1.2 s) | object returns to idle | ambient settles back | hand leaves proximity | Hovering must be consequence-free; protects the weight of the commit |
| Grab object (commit) | core | Heavy | — | optional low slow rumble (Void/Judgement) | mirror completes full morph (~3 s, EaseOutBack settle) | object dissolves inward; other two dim+fade (~1.5–2 s); portal births by Bloom swell (no flash) | confirm chime ±10–15% pitch + bed crossfade to theme | grasp/grab | The decisive act — full transform + closing of alternatives + portal |
| Portal birth | core | Heavy | — | — | EaseOutBack scale-settle from seed (~2.5–3 s) | doorway grows w/ slow Bloom halation, theme-tinted | low theme drone rises | follows commit | The reward/threshold made visible, comfort-eased |
| Walk through portal | core | Critical | — | — | — | slow fade-to-theme-color (~1.5–2 s SmoothStep), then scene load | bed resolves / silence into load | soul passes through doorway volume | Scene transition to Level 3 — the one allowed full-view change, kept slow for comfort |
| Unchosen pedestals dormant | optional | Minor | — | — | bob halts | glow dimmed to dark silhouette | — | post-commit | Quietly preserves the alternatives as presence without distraction |

### Sequences

- **Hover → preview:** hand nears object (~0.6 m) → object brightens + bob quickens, mirror partial-leans 30–40% toward theme (~0.5 s `SmoothStep`) → *(no commitment cue)* → hand withdraws → all eases home (~1.0–1.2 s). Fully reversible, repeatable across all three.
- **Grab → commit → portal → exit:** grasp (0 ms) → confirm chime + object dissolves inward → mirror completes full morph begins (~150 ms, ~3 s curve) → unchosen objects dim+fade (~150 ms–2 s) → portal seeds and grows (~600 ms, EaseOutBack, ~2.5–3 s to full, Bloom swell no flash) → theme bed crossfaded in (~3 s) → soul walks through doorway → slow fade-to-theme-color (~1.5–2 s) → Level 3 scene loads.

### Assets needed (choice mechanic)

- **Pedestal ×3 (core)** — matte near-black low plinth, identical, frame-into-room. *Match:* room base `#050506`, minimal. *Usage:* hold the three objects in a semicircle. *Size:* ~0.9 m tall, ~0.4 m top.
- **Void object — dark sphere / guttering candle (core)** — light-consuming matte body w/ dying-ember low-glow + smoke wisp. *Match:* home-cyan drained toward black; emissive-but-dim discipline. *Palette:* `#0A0E12` body, `#1E3A4A`→`#080C10` ember, rim `#2E5A6E`. *Size:* ~0.25 m.
- **Rebirth object — seed / egg with sprout (core)** — pale shell + green sprout, breathing green emissive + rising motes. *Match:* Bloom pollen feel, recolored green. *Palette:* shell `#E8E4D8`, glow `#4FC57A`, motes `#86E2A0`. *Size:* ~0.25 m.
- **Judgement object — split crystal / scale (core)** — cleanly bisected gold-light / red-dark, opposed-breathing halves, charged seam. *Match:* Bloom gold family + new wrath red. *Palette:* gold `#F2C24E`/white `#F7F4EC` vs red `#C8412E`/`#160A08`. *Size:* ~0.3 m.
- **Mirror states ×3 (core)** — Void (black/dissolve, reuses Decay shed), Rebirth (green bloom, reuses Bloom recolored), Judgement (vertical split, new). *Match:* existing mirror morph system + palette above. *Usage:* the committed transform.
- **Portal ×3 (core)** — Void rift, Rebirth living ring, Judgement split door; each a walk-through doorway. *Palette:* per portal column above. *Usage:* threshold to Level 3. *Size:* ~2.2 m × 1.1 m.
- **Choice particle sets (core: object glow motes; optional: portal-edge particles)** — additive-glow, per-theme colors above, sparse and slow. *Match:* existing particle discipline (fewer/softer/longer-lived). *Usage:* around objects, mirror states, portal edges.
- **Confirm chime + 3 theme ambient beds (core: chime + beds; optional: low theme rumble/haptic)** — pitch-varied (±10–15%) grab confirm + cave-reverb beds crossfading to chosen theme. *Match:* existing Afterlife ambient-bed crossfade approach. *Usage:* commit + theme mood.
- **Screen fade-to-color (core)** — slow eased full-view fade per theme on walk-through (`#050506` Void / `#1A4A2C` Rebirth / `#6E561E` Judgement). *Match:* VR-comfort transition discipline — slow `SmoothStep`, never a flash. *Usage:* hand-off to Level 3 scene load.
