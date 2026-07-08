# Rythmbox

A cross-platform desktop **MIDI file player** and **SoundFont (.sf2) instrument player** built with
.NET and [Avalonia UI](https://avaloniaui.net/), styled after hardware rack workstations like the
Boss DR-880.

This is **Phase 1** of the project: a solid player. A DR-880-style step sequencer / rhythm
workstation (patterns, songs, kits, effects) is planned as a later phase — see
[Roadmap](#roadmap) below.

## Features

- Load a SoundFont (`.sf2`) bank and browse/search its presets.
- Audition presets with an on-screen piano keyboard, or connect a physical MIDI keyboard
  (cross-platform, via PortMidi) and play it live through the loaded SoundFont.
- Load and play Standard MIDI Files (`.mid`) through the SoundFont synthesizer, with
  play/pause/stop, seek, and loop.
- Per-channel "track" panel with mute, solo, and volume for the loaded MIDI file.
- Master strip: master volume, RMS/Peak level meter, and output device selection.

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
                     MidiFilePlayer, MidiInputService.
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

Then use **Load SoundFont (.sf2)...** in the app to open it.

## Roadmap

Planned for a later phase, not part of this initial player release:

- DR-880-style step sequencer: 16-step pattern grid per drum voice, with drum kits mapped from
  the SoundFont's percussion bank.
- Pattern bank and song chaining (arrange patterns into a full song), swing/tempo controls,
  intro/ending and fill buttons.
- Real-time pattern recording and quantization from the already-wired live MIDI input.
- Per-track effects (reverb/chorus/EQ, via SoundFlow's effects components) and a fuller mixer view.
- Saving/loading Rythmbox projects (patterns, songs, mixer state).
