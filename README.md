# Rythmbox

A cross-platform desktop **MIDI file player** and **SoundFont (.sf2) instrument player** built with
.NET and [Avalonia UI](https://avaloniaui.net/). The UI is styled after hardware drum-machine
workstations like the Boss DR-880: a "DrumStage" layout with a live clock, a big now-playing box,
a General MIDI percussion pad grid, and a MIDI loop browser.

This is **Phase 1** of the project: a solid player. A DR-880-style step sequencer / rhythm
workstation (patterns, songs, kits, effects) is planned as a later phase — see
[Roadmap](#roadmap) below.

## Features

- **Header**: live clock, an AUDIO status badge, a one-click SoundFont loader, and the current
  loop's tempo readout.
- **Now Playing**: loop position in the browser (e.g. `01/03`), PLAYING/STOPPED status, loop name,
  big BPM readout, and a playback progress bar, with next/previous loop buttons.
- **Percussion pad grid**: 19 pads mapped to the standard General MIDI percussion note numbers
  (Kick, Snare, hats, cymbals, toms, congas, cowbell, tambourine, etc.) — works with any
  GM-compatible SoundFont. Click a pad (or press `1`-`8` on the keyboard) to trigger it live. Pads
  actually used by the currently loaded loop are highlighted with a red border.
- **MIDI Loop Browser**: point it at a folder of `.mid`/`.midi` files; each loop is listed with its
  hit count, duration, and detected BPM. Selecting a loop loads and plays it immediately.
- **Transport**: a big STOP/PLAY button, SUB (loop toggle), MIXER (opens the mixer overlay),
  RESCAN, QUIT, and a MIDI-input connect button.
- **Mixer overlay** (via the MIXER button): SoundFont preset browser with search and an on-screen
  piano keyboard for auditioning melodic instruments, live MIDI input device selection, a
  per-channel mixer (mute/solo/volume) for the loaded loop, and the master strip (master volume,
  RMS/Peak level meter, output device selection).
- Connect a physical MIDI keyboard (cross-platform, via PortMidi) and play it live through the
  loaded SoundFont.

### Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Space` | Play / pause the current loop |
| `1`-`8` | Trigger percussion pads 1-8 |
| `Esc` | Quit |

> The reference DrumStage layout also has section (`A`/`B`/`C`/`D`), intro/ending (`I`/`E`), and
> "full" (`` ` ``) shortcuts. Those depend on pattern/section data that a plain Standard MIDI File
> doesn't carry, so they aren't implemented in this MIDI-file-based player — they're natural fits
> for the Phase 2 pattern/song sequencer (see [Roadmap](#roadmap)).

## Tech stack

- **.NET 10** / C#
- **Avalonia UI 11+** (Fluent dark theme) with the MVVM pattern (`CommunityToolkit.Mvvm`)
- **[SoundFlow](https://github.com/LSXPrime/SoundFlow)** — cross-platform audio engine (MiniAudio
  backend) providing SoundFont synthesis, MIDI file sequencing, and MIDI device I/O (via
  `SoundFlow.Midi.PortMidi`) in one MIT-licensed package.

## Project layout

```
Rythmbox.slnx
src/
  Rythmbox.Core/     Engine layer (no UI dependency): PlaybackEngine, SoundFontPlayer,
                     MidiFilePlayer, MidiInputService, LoopLibraryService, GmPercussionMap.
  Rythmbox.App/      Avalonia application: Views, ViewModels, styling.
```

## Building and running

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (or newer) with desktop support.

```bash
dotnet restore
dotnet build
dotnet run --project src/Rythmbox.App
```

## Getting a SoundFont

SoundFont files (`.sf2`) are **not bundled** with this project since most are separately licensed
or copyrighted. To try Rythmbox out, download a free General MIDI SoundFont, for example:

- [FluidR3_GM.sf2](https://member.keymusician.com/Member/FluidR3_GM/index.html)
- [GeneralUser GS](https://schristiancollins.com/generaluser.php)

Then use **Load SoundFont...** in the header to open it. Rythmbox automatically arms the standard
GM drum kit (bank 128, program 0) on the percussion channel so the pad grid sounds correct right
away.

## Getting MIDI loops

Use **Browse Folder...** in the MIDI Loop Browser to point Rythmbox at a folder of `.mid`/`.midi`
drum loops. Each file is scanned for its note-hit count, duration, and tempo, and listed for
one-click playback.

## Roadmap

Planned for a later phase, not part of this initial player release:

- DR-880-style step sequencer: 16-step pattern grid per drum voice, with drum kits mapped from
  the SoundFont's percussion bank.
- Pattern bank and song chaining (arrange patterns into a full song), swing/tempo controls,
  section (A/B/C/D), intro/ending, and fill buttons.
- Real-time pattern recording and quantization from the already-wired live MIDI input.
- Per-track effects (reverb/chorus/EQ, via SoundFlow's effects components) and a fuller mixer view.
- Saving/loading Rythmbox projects (patterns, songs, mixer state).
