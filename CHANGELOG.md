# Changelog

## 6.2 – Keyboard effects, sound colours, and the SOUND tab
The mode list is now five entries instead of eleven: **Pulse**, **Sound Sync #1 (Vibe)**, **Sound Sync #2 (Beat)**,
**Fade out** and **Custom**. The waves, Rise and Overall entries are gone; old saved pattern numbers are mapped onto
the closest surviving mode when the app starts.

- **New SOUND tab on four-zone RGB models** (`DASH | SOUND | COLOR | KEYS | REACT | SETTINGS`). It holds only the
  two Sound Sync engines (**Vibe** and **Beat**), their description, sensitivity, the live level meter, the five
  colour styles and the same Start / Stop button as DASH. Pulse, Fade out and Custom stay on DASH, and a shortcut
  that selects one of them while SOUND is open returns to DASH.
- **Sound colours (RGB only):** five styles — **Single**, **Duo**, **Warm**, **Cool** and **Rainbow** — split the
  four zones while either Sound Sync engine runs. The row is built only when the colour controller answers, so it
  never appears on brightness-only laptops. Single keeps the one-packet path; the other styles send one packet per
  zone on each level change.
- The SOUND tab shares the sensitivity value with DASH, so either page can be used without the two drifting apart.
  Brightness-only models have no SOUND tab; their Vibe and Beat modes remain in the DASH pattern selector.

The COLOR tab gains an **EFFECT** list under the colour swatches:

- **Firmware effects:** **Breathing**, **Neon**, **Wave**, **Ripple**, **Zoom**, **Snake** and **Disco**. These are
  the controller's own animations (the bytes Nitrous/OpenRGB use): one packet starts them, the keyboard animates
  itself, and the app sends nothing more.
- **Rainbow flow:** the spectrum scrolls left to right across the four zones, drawn by the app so models whose
  firmware has no rainbow mode still get a full-colour animation.
- **Rainbow wave:** the spectrum stands still while a brightness wave runs through it, which reads as a vertical
  wipe on the four horizontal zones.
- The choice is saved in `HKCU\Software\KbBreathe\Effect` and restored on the next launch.
- Only one thing owns the LEDs: picking an effect while breathing, a reaction or screen sync is running stops that
  first, and stopping breathing or sync puts the chosen colour and effect back.
- Picking a colour while an effect runs re-sends the effect in the new colour, no restart needed.
- Software flows drop to about 8 fps while the window is hidden in the tray, so the app stays light; the firmware
  effects cost nothing after their single packet.
- The window and colour page are unchanged in size (420 x 510). Brightness-only models get neither SOUND nor COLOR,
  no effect list and no Sound colours row; the strip stays `DASH | KEYS | REACT | SETTINGS`.

## 6.1 – More Acer models, colour, and screen sync
KB Breathe now probes the same keyboard interfaces as [Nitrous](https://github.com/jeremyaliparo/nitrous), so it works
on more than the four-step Nitro path:

- **New tab strip:** `DASH | COLOR | KEYS | REACT | SETTINGS`. The **COLOR** tab is built only when the keyboard has
  the colour controller; on every other model it does not exist and the strip stays
  `DASH | KEYS | REACT | SETTINGS`, the same way Nitrous drops its light page. The nav bar now takes its tab list at
  run time, so the two layouts map to the right pages.
- **ENEK colour controller** (`0CF2:5130`): four-zone RGB. The device is opened once and kept open while a pattern
  runs; colours go out as the controller's own Static packets (one per zone when the zones differ).
- **Keyboard colour:** nine swatches on the COLOR tab, saved to `HKCU\Software\KbBreathe\Color`. Breathing patterns,
  reactions and Sound Sync run in that colour. A change while a pattern runs applies on the next step; picking a
  colour while stopped applies it straight away.
- **Screen colour sync** (ported from Nitrous' `LightingSync` screen source): `StretchBlt` scales the primary display
  into a 64x36 DIB (`CAPTUREBLT`, retried as plain `SRCCOPY` if a driver refuses it), the four screen quarters become
  the four zone colours, and frames are written every 125 ms. A live four-zone preview sits under the toggle.
- **One owner of the LEDs:** pressing **Start** stops screen sync, turning sync on stops the pattern or Sound Sync,
  and reaction pulses pause sync frames instead of fighting them. Stopping sync restores the chosen colour.
- **Acer WMI `APGeAction`:** the backlight path of the WMI-only models (Nitro V 15 `ANV15-41` and similar), addressed
  with the keyboard hotkey Acer's agent stores in the registry (`BK_Hotkey_Number`, default 132) and keeping the
  auto-off timeout intact. Detection requires a real answer (hotkey echo or a firmware brightness step), so a BIOS
  that returns a constant is not mistaken for support.
- **Backend reporting:** every write now goes through one dispatcher, and SETTINGS → BACKEND names the path in use:
  `RGB`, `EC`, `WMI KB` or `WMI`.
- The existing **Acer EC HID** and **`Acer_Gaming`** paths are unchanged. The Sound Sync modes, their analysis and
  their tuning are untouched.
- `nitrous-master/` is reference source only and is excluded from compilation in `KbBreathe.csproj`, so the app keeps
  building as the single `Program.cs` project.

## 6.0.2 – App icon
The exe now has its own icon (`icon.ico`), shown in Explorer, the taskbar, the startup list and the tray.
The window and tray icon are stored inside the program code, so no extra file is needed at run time. New 1280x640 social preview image.

## 6.0.1 – Sound Sync-Vibe
A third sound mode (pick **Sound Sync-Vibe** in the Pattern list, it is part of the cycle shortcut too). Three layers
are stacked and the brightest one wins:

| What you hear                                    | Keyboard level |
|--------------------------------------------------|----------------|
| Beat: kick / snare hit                           | **4** (100 %), held ~110 ms, then straight back down |
| Soft / ghost hit                                 | **3** (75 %) |
| Highs burst: hi-hat, cymbal, "s" (above 5 kHz)   | **3** (75 %), ~120 ms |
| Voice / sustained mids, **higher** pitch         | **2** (50 %) |
| Voice / sustained mids, **lower** pitch          | **1** (25 %) |
| Silence                                          | **0** (off) |

Built to feel clean rather than flickery:
- Rises are instant so hits land on the beat; a level is never dropped sooner than 60 ms after it was set.
- The beat no longer walks 4-3-2-1 after every hit: it holds, then goes straight to what is still playing.
- Hats are recognised as highs (not beats); kick leakage is not mistaken for a singer; steady noise / pads do not
  fake beats; a held note does not fade out.
- Pitch is relative to the song's own centre, so it works across songs. A band split cannot tell a singer from a
  guitar or synth lead, so "voice" means whatever sustained sound sits in 300 Hz - 3.4 kHz.
- The tuning constants are at the top of the SoundSync class (BeatHold, HiHold, MinDwell, SoftHit, VOn/VOff...).

## 6.0 – Sound sync
Pick **Sound Sync-Beat** or **Sound Sync** in the Pattern list (both are part of the cycle shortcut). The keyboard
brightness follows whatever your PC is playing, using the same 5 levels (0-1-2-3-4):
- **Sound Sync-Beat** – beat only: a kick or snare lights the keys for ~80 ms and then walks down 4-3-2-1, while
  sustained music (pads, vocals, held bass) leaves them dark instead of flickering along with the tune.
- **Sound Sync** – overall: follows the whole tune's loudness and dynamics, smoothly, not only the beat.
- Captures system audio with Windows' built-in loopback (no extra files, no drivers, nothing to install).
- Adapts automatically to the song's loudness and dynamics, so it uses the full 0-4 range on quiet and loud tracks.
- **Sensitivity** slider (1-10) and a live level meter. Silence = level 0.
- It follows the default playback device that was active when you pressed Start. After switching speakers/headphones,
  press Stop/Start (or re-select the sound mode).

## 5.x
- **Live changes**: switch pattern, edit the custom sequence, or change speed while breathing is on – it applies
  instantly, no Stop/Start needed.
- **Linear-style UI** (420x510): #0D0E11 background, 1px #24262E hairlines, white / #A1A1AA text, tracked-out caps labels,
  near-square corners, monochrome controls. The only accent is the indigo->violet gradient on the Start button.
  Uses *Inter* if it is installed, otherwise Segoe UI.
- Header shows only `KB BREATHE` and the ON / IDLE status. DASH / KEYS / SETTINGS tabs below it. No footer.
- DASH: app title card (KB Breathe 5.0 - Light control for Acer Nitro), pattern, speed, Start/Stop.
- SETTINGS: LEVEL / BACKEND / MODE status list plus the startup options (launch at Windows startup, start minimized,
  keep running in tray on close, and **apply selected mode on startup** – switches the saved pattern on by itself).
- **KEYS page – global shortcuts** (work even when the window is hidden in the tray):

  | Action          | Default      |
  |-----------------|--------------|
  | Cycle light mode| Ctrl+Alt+M   |
  | Start / stop    | Ctrl+Alt+B   |

  The cycle key steps through the light modes in order (Soft wave, Deep wave, ... Custom) and wraps around;
  it applies instantly while breathing.
  Click a shortcut, press the new combination. Esc cancels, Backspace clears. A shortcut needs at least one of
  Ctrl/Alt/Shift (F-keys may be used alone). If another program already owns a combination it is shown in red.

### Keyboard reactions (REACT page, added in 5.x)
Optional flashes for system changes you might not be looking at. Each one plays **0-1-2-3-4-3-2-1-0** on the keyboard
(~0.6 s) and then hands the keys back to whatever was running:
- **Pulse on power mode change** – the laptop's performance mode changes (Quiet / Balanced / Performance /
  Turbo, e.g. via the Nitrous hotkey). The keys step up one level at a time (180 ms per level), hold the height of
  the new mode for ~0.6 s, then cut straight to off: quiet `0-1`, balanced `0-1-2`, performance `0-1-2-3`, turbo
  `0-1-2-3-4` (0 = off, 1-4 = brightness). The mode is read from Nitrous' log, which records it the moment it is
  applied — the stored `Last*PowerMode` numbers lag a step behind. It falls back to the
  mode Nitrous saves for AC and battery, and falls back to the Windows power-mode slider when Nitrous is absent
  (the Acer WMI profile API needs administrator rights, so it is not used).
- **Pulse on charger plug-in** – AC power connected.
- **Pulse on charger unplug** – AC power removed.

All three are off by default. They are polled once a second instead of installing system hooks, and the first poll only
records the current state, so nothing fires when the app starts.
