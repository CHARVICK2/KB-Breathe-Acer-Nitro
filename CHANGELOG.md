# Changelog

## 6.1.1 – App icon
The exe now has its own icon (`icon.ico`), shown in Explorer, the taskbar, the startup list and the tray.
The window and tray icon are stored inside the program code, so no extra file is needed at run time. New 1280x640 social preview image.

## 6.1.0 – Sound Sync-Vibe
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
