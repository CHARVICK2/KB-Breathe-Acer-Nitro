<div align="center">

<img src="banner.png" alt="KB Breathe: Acer Nitro keyboard backlight that breathes and reacts to your music" width="100%">

<br>

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0D0E11?style=for-the-badge&logo=windows&logoColor=white)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8%20(built%20in)-0D0E11?style=for-the-badge&logo=dotnet&logoColor=white)
![MIT license](https://img.shields.io/badge/license-MIT-6366F1?style=for-the-badge)
![Version 6.2](https://img.shields.io/badge/version-6.2-6366F1?style=for-the-badge)
![Free](https://img.shields.io/badge/price-free-A855F7?style=for-the-badge)

[![Download KB Breathe](https://img.shields.io/badge/%E2%AC%87%20Download-latest%20version-6366F1?style=for-the-badge)](../../releases/latest)

**6.2 adds keyboard effects, an RGB-only SOUND tab, and sound colours.**

**One tiny `.exe`. No installer. No drivers. Nothing else to install.**

</div>

---

## Why KB Breathe?

Your Acer Nitro keyboard has four brightness steps, and NitroSense lets you pick one. **KB Breathe makes the light move.** It cycles through the steps in smooth breathing patterns, or follows whatever your PC is playing, so your keys pulse with the kick, flicker with the hats and glow with the vocals.

On models with the four-zone colour controller it does the same in colour, and it can paint those four zones from the four quarters of your screen while you work or play.

It lives in your system tray, starts with Windows if you want, and has global shortcuts so you never need to open a window.

## Features

- 🌬 **Five modes, no clutter.** Pulse, **Sound Sync #1 (Vibe)**, **Sound Sync #2 (Beat)**, Fade out, or write your own sequence with **Custom**. Five speed presets from *Glacial* to *Turbo*.
- 🎵 **A SOUND tab on four-zone RGB keyboards.** The two Sound Sync engines, the sensitivity slider, the live meter and the start/stop button sit together on **SOUND**. Works with anything playing on your PC: Spotify, YouTube, games, local files. Pulse, Fade out and Custom stay on **DASH**, where every model can reach them.
- 🌈 **Sound colours on RGB keyboards.** Five styles — Single, Duo, Warm, Cool and Rainbow — split the four zones while Sound Sync runs. They live on the **SOUND** tab and exist only on keyboards with the four-zone colour controller.
- 🎨 **COLOR tab on RGB keyboards.** Pick a colour for the patterns, reactions and Sound Sync, pick an **effect** — the controller's own Breathing, Neon, Wave, Ripple, Zoom, Snake and Disco, plus **Rainbow flow** and **Rainbow wave** — or turn on **Screen colour sync**: the four zones take the colour of the four quarters of your screen and follow it eight times a second, like Nitrous. All of it lives on a tab that only exists when the hardware answers, so models without the controller keep the four-tab strip and no COLOR tab.
- ⌨ **Global shortcuts.** Change mode or start/stop from anywhere, even with the window hidden.
- 🔔 **Keyboard reactions.** A little flash when you change power mode, plug in the charger, or unplug it.
- ⚡ **Live changes.** Switch pattern, speed or sensitivity while it runs. It applies instantly.
- 🪶 **Tiny and quiet.** One exe of about 180 KB, using the .NET Framework that is already part of Windows.
- 🎛 **Clean dark UI** with a live level meter, tray icon and startup options.

## Sound Sync

<div align="center">
<img src="vibe-levels.png" alt="Sound Sync-Vibe maps beats to level 4, highs to 3, voice to 1 and 2, and silence to 0" width="760">
</div>

<br>

Pick **Sound Sync #1 (Vibe)** from the DASH pattern selector, or from the **SOUND** tab on a four-zone RGB keyboard. Three layers run together and the brightest one wins:

- **Beat → 4.** A kick or snare punches the keys to full for a moment, then drops straight to whatever is still playing.
- **Highs → 3.** Hi-hats, cymbals and "s" sounds. Soft or ghost hits also land here.
- **Voice → 1 or 2.** Sustained vocals or leads light the keys softly; a **higher pitch** goes brighter than a **lower pitch**.
- **Silence → 0.** Nothing playing, keys off.

It adapts to each song's loudness on its own, and the **Sensitivity** slider (1 to 10) lets you tune it. Levels never chatter: a level is held for at least 60 ms before it can drop, so it feels like rhythm, not flicker.

| Mode | What it does | Best for |
|---|---|---|
| **Sound Sync #1 (Vibe)** | Beat, highs and voice each get their own brightness | Music with vocals, general "vibe" |
| **Sound Sync #2 (Beat)** | Only kick/snare hits flash the keys, sustained sound stays dark | Electronic, drums, bass-heavy tracks |

### Sound colours (RGB only)

On a keyboard with the four-zone colour controller, the bottom of the **SOUND** tab has five **Sound colours** styles:

| Style | What the four zones do |
|---|---|
| **Single** | The current COLOR swatch, one packet, exactly as before. |
| **Duo** | The chosen colour and white alternate across the zones. |
| **Warm** | Orange, amber, pink and gold. |
| **Cool** | Cyan, blue, violet and teal. |
| **Rainbow** | One quarter of the spectrum per zone. |

The style applies while **Sound Sync #1** or **#2** is running; the brightness still follows the audio. Pulse, Fade out
and Custom keep using the single COLOR colour. Brightness-only models have no **SOUND** tab and no **Sound colours**
row; their two Sound Sync modes stay available in the DASH pattern selector.

> **Heads-up:** a frequency split can't tell a singer from a guitar or synth lead, so "voice" means any sustained sound between 300 Hz and 3.4 kHz. Pitch is relative to each song's own range.

## Pages

| Tab | What is on it |
|---|---|
| **DASH** | Pattern, speed, live preview and Start / Stop. |
| **SOUND** | Four-zone RGB keyboards only: Vibe / Beat, sensitivity and live meter, plus the five **Sound colours** styles. |
| **COLOR** | Keyboard colour, keyboard effects and screen colour sync. Only exists when the keyboard has the four-zone colour controller. |
| **KEYS** | The two global shortcuts, rebindable. |
| **REACT** | The optional keyboard flashes for power-mode and charger changes. |
| **SETTINGS** | Live status (level, backend, mode) and the startup options. |

On brightness-only keyboards neither **SOUND** nor **COLOR** is built, so the strip stays **DASH | KEYS | REACT | SETTINGS**. Their two Sound Sync engines remain in the DASH pattern selector; there is no colour row to show on a single-colour backlight.

## Colour and screen sync

The COLOR tab appears when KB Breathe finds the keyboard's colour controller (the same ENEK interface Nitrous
drives). It has three parts.

**Keyboard colour.** Nine swatches, from white through warm and cool to pink. The choice is saved, and the breathing
patterns, reactions and Sound Sync then run in that colour instead of white. Picking a colour while a pattern runs
takes effect on the next step, so nothing has to be restarted. The **SOUND** tab's **Sound colours** row can also
split that colour across the four zones while Sound Sync runs (see below).

**Keyboard effects.** The **EFFECT** list under the swatches runs the controller's own animations in your chosen
colour: **Breathing**, **Neon**, **Wave**, **Ripple**, **Zoom**, **Snake** and **Disco**. These are firmware modes, so
one packet starts them and they keep going. **Rainbow flow** (the spectrum scrolling left to right across the zones)
and **Rainbow wave** (the spectrum with a brightness wave running through it) are drawn by KB Breathe itself, 25
frames a second, so any RGB model gets full-spectrum motion even when its firmware has no rainbow mode. The choice is
saved in `HKCU\Software\KbBreathe\Effect`. Effects yield to patterns, reactions and screen sync, and come back when
those stop. The firmware effects cost nothing after their single packet; the two app-drawn flows run at 25 fps while
the window is open and drop to about 8 fps when it is hidden in the tray, so the app stays light either way.

**Screen colour sync.** Turn on **Follow the screen colours** and the keyboard copies the screen: each of the four
zones takes the average colour of one vertical quarter of the primary display, refreshed eight times a second.

| Zone | Follows |
|---|---|
| 1 | the left quarter of the screen |
| 2 | the second quarter |
| 3 | the third quarter |
| 4 | the right quarter |

The four swatches under the toggle show the colours being sent, live. Sync and the pattern engine never run at the
same time: pressing **Start** stops sync, and turning sync on stops the pattern or Sound Sync first. Turning sync off
puts the keyboard back on your chosen colour and effect. Only the primary display is read, and the Windows secure desktop (UAC
prompts, the lock screen) cannot be captured, so the keyboard holds its last frame until the desktop is back.

## Quick start

1. Download **`KbBreathe.exe`** from the [latest release](../../releases/latest).
2. **Close NitroSense** (it can lock the keyboard device).
3. Double-click `KbBreathe.exe`, pick a pattern, press **Start**.

That's it. Close the window and it keeps running in the tray.

<!--
SCREENSHOTS: take 2-3 screenshots of the app and upload them next to README.md (e.g. screenshot-dash.png).
Then remove the comment wrapper around this block (the first and last line of it)
so the images below show up on your page.

<div align="center">
<img src="screenshot-dash.png" width="300"> <img src="screenshot-keys.png" width="300">
</div>
-->

## Shortcuts

Work everywhere, even when the window is hidden in the tray. You can change them on the **KEYS** page.

| Action | Default |
|---|---|
| Cycle light mode | `Ctrl` + `Alt` + `M` |
| Start / stop | `Ctrl` + `Alt` + `B` |

## Keyboard reactions

Optional flashes for system changes you might not be looking at. Each plays 0-1-2-3-4-3-2-1-0 on the keyboard (about 0.6 s), then hands the keys back to whatever was running. All off by default.

- **Power mode change** (Quiet / Balanced / Performance / Turbo, e.g. via the Nitro key). The keys step up to the height of the new mode: Quiet 1, Balanced 2, Performance 3, Turbo 4.
- **Charger plugged in**
- **Charger unplugged**

## Compatibility

KB Breathe drives the keyboard through whichever Acer interface your model has, using the same three paths
[Nitrous](https://github.com/jeremyaliparo/nitrous) probes:

| Path | What it gives you | Seen on |
|---|---|---|
| ENEK colour controller (HID) | **Colour** plus the 0-4 brightness steps | Nitro 16S (`AN16S-61`) and other four-zone models |
| Acer EC HID | 0-4 brightness steps | 2024+ Nitro / Predator |
| Acer WMI (`APGeAction`, then `Acer_Gaming`) | 0-4 brightness steps | Nitro V 15 (`ANV15-41`) and other WMI-only models |

The **SOUND** and **COLOR** tabs are only built when the four-zone colour controller is present; on the
brightness-only models they are hidden, the same way Nitrous hides its light page, and the strip stays
**DASH | KEYS | REACT | SETTINGS**. Patterns, reactions and Sound Sync work on all three paths: without the
controller, pick Vibe or Beat from the DASH pattern selector.

The path in use is shown on the **SETTINGS** page as **BACKEND** (`RGB`, `EC`, `WMI KB` or `WMI`), so an issue report
can say exactly which interface answered.

**Does it work on your Nitro?** Please [open an issue](../../issues) and tell me your model, so I can build a list.

## Troubleshooting

| Problem | Fix |
|---|---|
| *"No backlight interface found"* | Close **NitroSense** / OpenSense completely (check the tray), then try again. |
| Sound Sync doesn't react after changing speakers or headphones | It follows the device that was default when you pressed Start. Press **Stop**, then **Start**. |
| Sound Sync is too busy or too calm | Move the **Sensitivity** slider. Lower = calmer, higher = more reactive. |
| There is no **COLOR** tab | Your keyboard has no colour controller; the tab is hidden on brightness-only models on purpose. **SETTINGS → BACKEND** shows which path was found. |
| Colour or screen sync does nothing | Close **NitroSense** / OpenSense completely, press **Stop**, then try again. The backend must read `RGB`. |
| Screen sync holds one frame | The secure desktop (UAC prompt, lock screen) cannot be captured. It picks up again when your desktop is back. |
| Windows or antivirus warns about the exe | It's a small unsigned hobby app that talks to hardware, which some scanners dislike. The full source is in this repo, so you can read it or build it yourself. |

## Build from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) (6, 7, 8 or 9) on Windows.

```bat
build.bat
```

This builds `dist\KbBreathe.exe`. The project is a single file, `Program.cs`, with no dependencies.

The `nitrous-master/` folder in this repo is reference source (the upstream Nitrous app, WPF/.NET 10). It is excluded
from compilation in `KbBreathe.csproj`, so it never ends up in KB Breathe and does not need to build.

## Contributing

Ideas, bug reports and pull requests are welcome. If a song feels wrong in Sound Sync #1 (Vibe), open an issue with the song and what you expected to see. The tuning constants sit at the top of the `SoundSync` class. For screen sync, the frame rate (125 ms) is at the top of `StartSync`, and the sampling grid is `Columns` / `Rows` in `ScreenZones`.

## Disclaimer

KB Breathe is an independent hobby project and is **not affiliated with or endorsed by Acer**. It sends the same kind of backlight commands as NitroSense, but you use it **at your own risk**.

## License

[MIT](LICENSE). Free to use, change and share.

---

<div align="center">
<sub>If KB Breathe made your keyboard feel alive, a ⭐ on the repo helps other Nitro owners find it.</sub>
</div>
