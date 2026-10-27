# Look Dev Rig

Wygodne sterowanie kamerą i światłem w edit mode i w Play mode.

- `LookDevRig.cs` — komponent na scenie (runtime + overlay w Play mode)
- `../Editor/LookDevWindow.cs` — okno edytora i kontrolki w Scene view

## Start

1. `Tools > Open Look Dev`
2. **Create Look Dev Rig** — rig sam znajdzie Main Camera i directional light i wczyta ich obecne ustawienia.

Kamera krąży wokół **pivota** (yaw / pitch / distance / FOV). Światło ma własny yaw / pitch, intensity, kolor i shadow strength. *Follow camera* liczy yaw światła względem kamery.

> Dopóki rig jest włączony, to on ustawia transform kamery i światła.
> Po ręcznym przesunięciu kliknij **Sync from scene** albo wyłącz komponent.

## Edit mode

**Okno Look Dev:**

| Przycisk | Co robi |
|---|---|
| Sync from scene | wczytuje obecną pozycję kamery i światła |
| Pivot → selection | kadruje zaznaczone obiekty |
| Frame all | kadruje wszystkie renderery |
| Scene view → camera | ustawia kamerę tak jak Scene view |
| Camera → scene view | ustawia Scene view tak jak kamerę |

**Scene view** (przy otwartym oknie Look Dev):

- uchwyt pozycji = pivot
- żółta strzałka = kierunek światła
- **L + przeciągnij LMB** — obrót światła

Wszystkie zmiany obsługują Undo.

## Play mode

| Wejście | Akcja |
|---|---|
| LMB drag | orbita kamery |
| RMB / Shift+LMB drag | obrót światła |
| MMB drag | pan pivota |
| Scroll | zoom |
| Space | turntable kamery |
| L | turntable światła |
| F | frame all |
| R | reset do stanu z wejścia w Play |
| H | pokaż / ukryj panel z suwakami |

Turntable działa tylko w Play mode.

**Keep values after Play** (w oknie, podczas Play) — przenosi aktualne wartości do sceny po wyjściu z Play.
