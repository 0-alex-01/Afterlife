# Afterlife — Narrator Lines (Ebene 2: Baum · Friedhof · Spiegelraum)

**Sprache der Lines:** Englisch.
**Ton:** wie Bennett Foddy in *Getting Over It* — ruhig, warm, trocken-amüsiert, nie albern.
Witze werden gesprochen, als wären sie keine. Bei den ernsten Zeilen: langsamer, leiser.
Pausen („…") sind Teil des Textes und sollen hörbar sein.

**Sprecher-Regie:** gelassene Erzählstimme, leichtes Schmunzeln in den verspielten Zeilen,
ehrliche Ruhe in den ernsten. Kein Pathos, kein Grusel. Die Stimme kennt den Spieler nicht —
aber sie mag ihn.

**Technik-Hinweis:** Jede Zeile = ein eigener Audio-Clip. Dateinamen-Vorschlag:
`Narrator_<Szene>_<Trigger>.wav` (z. B. `Narrator_Tree_Enter01.wav`).
Cooldowns einplanen (min. 20–30 s zwischen Idle-Lines), Anweisungen nur einmal abspielen.

---

## 🌳 Tree_Scene — The Memory Spiral

| Trigger (vorhandenes System) | Line |
|---|---|
| Szenenstart (nach Fade-In, ~3 s Ruhe) | "Welcome back. Or welcome… away? Prepositions get tricky once you're dead." |
| Szenenstart, Folgezeile (~8 s später) | "Ahead of you: a tree. Behind you: your entire life. I recommend the tree." |
| Erste Anweisung (falls Spieler ~15 s nicht klettert) | "Grab a hold. Pull yourself up. Don't let go until you've found the next one. That's all there is to it. Between us — that was the entire instruction manual for being alive, too." |
| Erster Kletter-Grab (ClimbHold berührt) | "Good. There are ramps as well, for the cautious. No judgement. … A little judgement." |
| Erster Sturz (Höhenverlust > ~4 m) | "Falling doesn't hurt anymore. That's one of the three perks of being dead. I'll tell you the other two later." |
| Zweiter Sturz | "Again. You know — memories work the same way. You fall out of them, and you climb back in." |
| Halbe Höhe (AfterlifeAscensionController, ~50 %) | "Do you feel that? It's getting warmer. Not the air. You." |
| Idle > 45 s | "Take your time. I have plenty. You, strictly speaking, have an infinite amount. Eternity is one of our core products here." |
| Kurz unter der Krone (~90 % Höhe) | "Almost there. From up here, your life looks small. It wasn't small. Just far away." |
| Portal erwacht (TreePortalUnlock, Kopfhöhe 66,5) | "There. It's awake. It's been waiting for you — not the way one waits for a bus. The way one waits for someone." |

## 🕯️ Cemetery — Cemetery of Unfinished Goodbyes

*Hier bewusst weniger Witze — die Stimme wird an den ernsten Stellen still.*

| Trigger | Line |
|---|---|
| Szenenstart | "A graveyard. Yes, I know. A little on the nose, for a dead person. But you're not here to lie down." |
| Erste MourningFigure in Sichtweite (FigureProximityReveal) | "Do you see them? They're grieving. For whom doesn't matter. Grief has no address." |
| Anweisung (nach der Sicht-Zeile) | "Go to them. Lay your hand on them — and hold it there. Not briefly. Hold it until it's enough. You'll know when it's enough." |
| Nach 1. Erlösung (CemeteryFigure released, Kerze an) | "There. The candle. Comfort doesn't disappear. It only changes shape." |
| Bei 2. Figur, optional (die eine lockere Zeile) | "In case you're wondering whether a translucent hand can even give comfort: it's the only one here that can. Congratulations on the promotion." |
| Nach 2. Erlösung (Nebel dünnt aus) | "The fog is thinning. That's not you doing that. … Actually. Yes it is." |
| Idle bei leeren Gräbern > 30 s | "The quiet graves? Stories too. Some goodbyes have simply finished being told." |
| Nach 3. Erlösung (CemeteryAtmosphereManager, Lichtspur) | "Three lights. And look — the path is showing itself. Letting go isn't really a task you can check off. You just checked it off anyway. Both things can be true." |
| Lichtspur erscheint, Folgezeile | "Follow the lights. They don't lead home. They lead onward. Around here, that's roughly the same thing." |

## 🪞 Mirrorroom — The Lake of Souls

*Der Spieler startet vor der verschlossenen Flügeltür (MirrorroomEntryGate) — der See wird erst nach dem Durchschreiten sichtbar. Die Eröffnung spielt mit dieser Schwelle.*

| Trigger | Line |
|---|---|
| Szenenstart (vor der Tür, nach Fade-In) | "A door. You climbed a tree. You crossed a graveyard. And now… a door. Don't worry. It's a very good door." |
| Anweisung (~8 s später, falls Spieler steht) | "It isn't locked. It was never locked. Doors like this only wait for one thing — for you to decide you're ready. Go on." |
| Idle vor der Tür > 30 s | "Take a breath. Figuratively, of course. Whatever is behind this door doesn't get easier while you stand here. It just gets… later." |
| Gate durchschritten (MirrorroomEntryGate.TriggerGate — Türen öffnen, Membran löst sich, Chime) | "There it is. The chime wasn't for the door, by the way. It was for you." |
| Erster Blick auf den See (~4 s nach dem Gate) | "A lake. A mirror. And in the mirror: you. Don't worry — it doesn't bite. It does something far worse. It waits." |
| Annäherung an den Spiegel (< ~3 m) | "That's you. Not as you were — as you are. That difference is the entire point of this room." |
| PedestalRiseSequence abgeschlossen | "Three pedestals, rising from the lake. Three futures. You only get one. That's not a rule, by the way. It's just how futures work." |
| Erster Hover (PedestalChoiceController.OnHoverEnter) | "You may touch them before you choose. The mirror shows you who you would become. Trying things on is allowed — the universe is surprisingly accommodating about it." |
| Zögern > 60 s ohne Commit | "Take your time. Most big decisions get made in three seconds and regretted for thirty years. Here, you're allowed to do it the other way around for once." |
| Zögern > 120 s (ernst) | "No one chooses for you. Least of all me — I'm just a voice. Honestly, I don't know where I come from either." |
| Commit (PedestalChoiceController.OnCommit) | "Chosen. No going back. … Finally, a door that's honest." |
| Lichtpfad entzündet (LakePathToPortal gestartet) | "The lake will carry you. Walk across the water. This is truly the single worst moment in recorded history to start having doubts." |
| Kurz vor dem Portal (< ~4 m) | "Whatever lies beyond: you picked it yourself. No one could say that about your beginning. They can about your ending." |
