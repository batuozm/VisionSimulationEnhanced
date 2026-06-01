# VisionSimulationEnhanced – Bedienung

Dieses Projekt enthält zwei vorbereitete VR-Szenen zur blickabhängigen Fokussimulation mit der VIVE Pro Eye:

- `Assets/Scenes/Baseline.unity`
- `Assets/Scenes/Enhanced.unity`

Die beiden Szenen besitzen denselben visuellen Aufbau. Der relevante Unterschied liegt im verwendeten Fokusalgorithmus:

- `Baseline` verwendet den `BaselineFocusProvider`
- `Enhanced` verwendet den `EnhancedFocusProvider`

Der `RefractiveErrorSimulator` ist in beiden Szenen standardmäßig deaktiviert, da er nicht Teil der vergleichenden Evaluation war.

## Voraussetzungen

Für die Nutzung mit der VIVE Pro Eye müssen folgende Voraussetzungen erfüllt sein:

- VIVE Pro Eye ist angeschlossen und betriebsbereit.
- SteamVR ist installiert und gestartet.
- SRanipal Runtime ist installiert und aktiv.
- Die Eye-Tracking-Kalibrierung der VIVE Pro Eye wurde durchgeführt, das geht allerdings erst nach Start der Szene.
- Unity-Version: `2022.3.19f1`
- Die benötigten XR-, OpenXR-, SteamVR-/VIVE- und SRanipal-Pakete sind bereits im Projekt enthalten.
- Das Scripting Define Symbol `USE_VIVE` muss gesetzt bleiben, damit der VIVE/SRanipal-Code kompiliert wird.

## Wichtige Unity-Einstellungen

Vor dem Start sollte geprüft werden, ob die XR-Einstellungen korrekt gesetzt sind:

1. `Edit > Project Settings > XR Plug-in Management`
   - OpenXR muss für die Desktop-/PC-Plattform aktiviert sein.

2. `Edit > Project Settings > XR Plug-in Management > OpenXR`
   - Play Mode OpenXR Runtime: `System Default`
   - Stereo Rendering Mode / Render Mode: `Multi Pass`

3. Die VIVE Pro Eye sollte in SteamVR korrekt erkannt werden.

4. In den Szenen muss das Objekt `EyeTrackingSystem` aktiv sein.
   - Die `EyeTrackingToolbox` sollte als Provider `HTC Vive S Ranipal` verwenden.
   - Die Kalibrierung kann über die Taste `C` ausgelöst werden.

## Szene starten

1. Unity öffnen.
2. Eine der beiden finalen Szenen öffnen:
   - `Assets/Scenes/Baseline.unity`
   - oder `Assets/Scenes/Enhanced.unity`
3. SteamVR starten und sicherstellen, dass die VIVE Pro Eye erkannt wird.
4. In Unity auf `Play` drücken.
5. Die VR-Brille aufsetzen.
6. Falls nötig, mit `C` die Eye-Tracking-Kalibrierung starten.
7. Danach kann die Szene normal verwendet werden.

## Bedienung in der Szene

Die Szene ist für eine sitzende oder stehende Nutzung mit der VIVE Pro Eye vorbereitet.

- Die Blickrichtung steuert die Fokusdistanz.
- Der `Defocus`-Effekt erzeugt die tiefenabhängige Unschärfe.
- In der `Baseline`-Szene wird der Fokus direkt aus dem zentralen Blickray bestimmt.
- In der `Enhanced`-Szene wird der Fokus durch zusätzliche Stabilisierungsschritte geglättet.

Der `CameraController` ist aktiv und kann genutzt werden, um die Nutzerposition im Editor beziehungsweise während Tests bei Bedarf über Tastatursteuerung anzupassen.

## Debug- und Logging-Komponenten

Folgende Komponenten sind in den finalen Szenen vorhanden:

- `FocusMetricsRecorder`
  - zeichnet Fokusmetriken automatisch beim Start der Szene auf.
- `GazeDebugger`
  - zeichnet Debug-Rays in der Unity Scene View.
- `GazeDebugDot`
  - ist standardmäßig deaktiviert und sollte für normale Tests ausgeschaltet bleiben.

Die aufgezeichneten Fokusmetriken werden im Ordner `FocusMetrics` gespeichert.

## Hinweise

- Für die finale Evaluation wurden nur die Szenen `Baseline` und `Enhanced` verwendet.
- Wenn kein Eye Tracking ankommt, zuerst prüfen:
  - Läuft SteamVR?
  - Läuft SRanipal?
  - Ist die VIVE Pro Eye korrekt erkannt?
  - Ist `USE_VIVE` gesetzt?
  - Steht die `EyeTrackingToolbox` auf `HTC Vive S Ranipal`?
  - Wurde die Eye-Tracking-Kalibrierung durchgeführt?