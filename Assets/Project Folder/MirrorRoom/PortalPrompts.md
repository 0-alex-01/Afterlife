# Portal-Prompts — See der Seelen (Mirrorroom)

Generierungs-Prompts für die drei Wahl-Portale, die nach der Entscheidung auf der
Portal-Insel (PortalIsland, z = +26) materialisieren. Gedacht für Text-zu-3D /
Bild-zu-3D (Tripo, Meshy …) und Bildgeneratoren (Membran-Texturen).

Gemeinsamer Stil-Anker (an alle Prompts anhängen):
> stylized dark fantasy VR art installation, elegant and minimal, ethereal glow,
> single object centered, neutral dark background, clean silhouette, game-ready,
> low-poly friendly, PBR, no text, no people

Maßstab beim Einbau: Portaldurchgang ca. 2,2–2,8 m hoch (VR-begehbar), steht auf
schwarzem Felssockel. Prefabs zum Ersetzen/Erweitern: `Portals/Portal_Void.prefab`,
`Portals/Portal_Rebirth.prefab`, `Portals/Portal_Judgement.prefab` (Materialien:
`PortalVoid.mat`, `PortalRebirth.mat`, `PortalJudgement_Gold.mat` + `_Red.mat`).

---

## 1) LEERE / VOID — das verlöschende Tor

**3D-Modell (Text-zu-3D):**
> a tall monolithic stone gateway dissolving into nothing at its top edges,
> smooth obsidian-black arch with thin engraved concentric rings, fragments of
> the arch breaking off and floating upward like slow ash, the doorway filled
> with a perfectly calm, light-swallowing black void with a faint deep-blue
> event-horizon rim, minimal, silent, weightless

**Membran-Textur (Bild, nahtlos/tileable):**
> seamless texture of a black event horizon, ultra-dark radial gradient swallowing
> light toward the center, faint deep blue rim glow at the outer edge, subtle slow
> ink-like swirls, no stars, matte void

**Stimmung/Farben:** Tiefblau (0.35, 0.5, 0.95) nur als schmaler Rand-Glow; sonst
lichtschluckendes Schwarz. Keine Partikel-Explosionen — Stille, Sog, Frieden.

---

## 2) WIEDERGEBURT / REBIRTH — das blühende Tor

**3D-Modell (Text-zu-3D):**
> a living archway grown from two intertwined young trees, fresh green sprouts
> and unfurling leaves along the branches, small glowing seed pods and petals
> drifting upward, the doorway filled with soft warm spring light like dawn
> through morning mist, moss and tiny flowers at the base, gentle spiral motif,
> hopeful and organic

**Membran-Textur (Bild, nahtlos/tileable):**
> seamless texture of soft green-golden light seen through young leaves, gentle
> bokeh of drifting petals and seeds, spring dawn glow, airy and warm, painterly
> gradient from emerald edges to warm ivory center

**Stimmung/Farben:** Frisches Grün (0.4, 0.95, 0.5) + warmes Morgenlicht in der
Mitte. Aufsteigende Samen/Blüten-Motes passen zum RebirthMotes-Partikelsystem.

---

## 3) GERICHT / JUDGEMENT — das zweigeteilte Tor

**3D-Modell (Text-zu-3D):**
> a majestic gateway split perfectly down the middle into two mirrored halves:
> the left half radiant polished gold with rising feather and halo motifs and
> warm heavenly light, the right half charred iron and ember-cracked basalt with
> descending chain and thorn motifs and smoldering red glow, a hanging balance
> scale at the apex joining both halves, the doorway filled with a vertical
> curtain of light split gold and crimson, symmetrical, monumental, solemn

**Membran-Textur (Bild, nahtlos an vertikaler Kante):**
> texture of a vertical curtain of light split exactly in half, left side warm
> golden rays rising upward like dawn through clouds, right side deep crimson
> embers sinking downward like slow fire, razor-sharp seam in the middle,
> dramatic, high contrast

**Stimmung/Farben:** Gold (1.0, 0.72, 0.28) links, Rot (1.0, 0.12, 0.14) rechts —
identisch zu den `judgementGold`/`judgementRed`-Farben des SoulMorphControllers,
damit Spiegel, Podestring und Portal dieselbe Sprache sprechen.

---

### Einbau-Notizen

- Generiertes Mesh als Kind unter das jeweilige `Portal_*`-Prefab (Visual ersetzen),
  Trigger/ScenePortal-Logik unangetastet lassen.
- Membran-Textur in das jeweilige `PortalSurface`-Material (Emission!), Frame-Motive
  in `PortalFrame`-Material.
- Die Portale zeigen mit der Front nach −Z (rotY 180 auf der Insel) — der Spieler
  kommt über den Wasserpfad vom Spiegel.
- Nach dem Import einmal `LakePathToPortal`-Farben prüfen (voidColor/rebirthColor/
  judgementColor), damit der Wasserpfad zur neuen Portal-Optik passt.
