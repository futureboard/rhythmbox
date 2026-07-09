#!/usr/bin/env python3
"""Generate a RhythmBox drum kit preset (JSON + WAV) from GM SFZ samples."""

from __future__ import annotations

import argparse
import json
import re
import shutil
from dataclasses import dataclass
from pathlib import Path

# Matches GmPercussionMap.Pads in src/Rythmbox.Core/Models/GmPercussionMap.cs
GM_PADS: list[dict] = [
    {"label": "A.Kick", "midi_note": 35, "sfz_key": 35, "choke_group": 0},
    {"label": "Kick", "midi_note": 36, "sfz_key": 36, "choke_group": 0},
    {"label": "Rim", "midi_note": 37, "sfz_key": 37, "choke_group": 0},
    {"label": "Snare", "midi_note": 38, "sfz_key": 38, "choke_group": 0},
    {"label": "Clap", "midi_note": 39, "sfz_key": 39, "choke_group": 0},
    {"label": "E.Snr", "midi_note": 40, "sfz_key": 40, "choke_group": 0},
    {"label": "F.Tom", "midi_note": 41, "sfz_key": 41, "choke_group": 0},
    {"label": "C.Hat", "midi_note": 42, "sfz_key": 42, "choke_group": 1},
    {"label": "H.F.Tm", "midi_note": 43, "sfz_key": 43, "choke_group": 0},
    {"label": "P.Hat", "midi_note": 44, "sfz_key": 44, "choke_group": 1},
    {"label": "L.Tom", "midi_note": 45, "sfz_key": 45, "choke_group": 0},
    {"label": "O.Hat", "midi_note": 46, "sfz_key": 46, "choke_group": 1},
    {"label": "M.Tom", "midi_note": 47, "sfz_key": 47, "choke_group": 0},
    {"label": "H.M.Tm", "midi_note": 48, "sfz_key": 48, "choke_group": 0},
    {"label": "Cym1", "midi_note": 49, "sfz_key": 49, "choke_group": 0},
    {"label": "H.Tom", "midi_note": 50, "sfz_key": 50, "choke_group": 0},
    {"label": "Ride", "midi_note": 51, "sfz_key": 51, "choke_group": 0},
    {"label": "China", "midi_note": 52, "sfz_key": 52, "choke_group": 0},
    {"label": "Rd.Bell", "midi_note": 53, "sfz_key": 53, "choke_group": 0},
    {"label": "Tamb", "midi_note": 54, "sfz_key": 54, "choke_group": 0},
    {"label": "Splash", "midi_note": 55, "sfz_key": 55, "choke_group": 0},
    {"label": "Cowbell", "midi_note": 56, "sfz_key": 56, "choke_group": 0},
    {"label": "Cym2", "midi_note": 57, "sfz_key": 57, "choke_group": 0},
    {"label": "Vibra", "midi_note": 58, "sfz_key": None, "choke_group": 0},
    {"label": "Ride2", "midi_note": 59, "sfz_key": 59, "choke_group": 0},
    {"label": "H.Bongo", "midi_note": 60, "sfz_key": None, "choke_group": 0},
    {"label": "L.Bongo", "midi_note": 61, "sfz_key": None, "choke_group": 0},
    {"label": "M.Cnga", "midi_note": 62, "sfz_key": None, "choke_group": 0},
    {"label": "O.Cnga", "midi_note": 63, "sfz_key": None, "choke_group": 0},
    {"label": "L.Cnga", "midi_note": 64, "sfz_key": None, "choke_group": 0},
    {"label": "H.Timb", "midi_note": 65, "sfz_key": None, "choke_group": 0},
    {"label": "L.Timb", "midi_note": 66, "sfz_key": None, "choke_group": 0},
    {"label": "H.Agogo", "midi_note": 67, "sfz_key": None, "choke_group": 0},
    {"label": "L.Agogo", "midi_note": 68, "sfz_key": None, "choke_group": 0},
    {"label": "Cabasa", "midi_note": 69, "sfz_key": None, "choke_group": 0},
    {"label": "Maracas", "midi_note": 70, "sfz_key": None, "choke_group": 0},
    {"label": "Sh.Wh", "midi_note": 71, "sfz_key": None, "choke_group": 0},
    {"label": "Lg.Wh", "midi_note": 72, "sfz_key": None, "choke_group": 0},
    {"label": "Sh.Gui", "midi_note": 73, "sfz_key": None, "choke_group": 0},
    {"label": "Lg.Gui", "midi_note": 74, "sfz_key": None, "choke_group": 0},
    {"label": "Claves", "midi_note": 75, "sfz_key": None, "choke_group": 0},
    {"label": "H.Wood", "midi_note": 76, "sfz_key": None, "choke_group": 0},
    {"label": "L.Wood", "midi_note": 77, "sfz_key": None, "choke_group": 0},
    {"label": "M.Cuica", "midi_note": 78, "sfz_key": None, "choke_group": 0},
    {"label": "O.Cuica", "midi_note": 79, "sfz_key": None, "choke_group": 0},
    {"label": "M.Tri", "midi_note": 80, "sfz_key": None, "choke_group": 0},
    {"label": "O.Tri", "midi_note": 81, "sfz_key": None, "choke_group": 0},
]

# Prefer close/direct mics for shells; overhead mics for cymbals.
MIC_PREFERENCES: dict[int, tuple[str, ...]] = {
    36: ("dir in kick", "dir out kick", "oh kick"),
    37: ("dir top snare",),
    38: ("dir top snare",),
    41: ("dir tom",),
    42: ("dir hihat",),
    45: ("dir tom",),
    46: ("dir hihat",),
    47: ("dir tom",),
    49: ("oh crash",),
    51: ("oh ride",),
    52: ("oh china",),
    57: ("oh crash",),
}

REGION_RE = re.compile(
    r"sample=(?P<sample>.+?\.wav)\s+"
    r"lovel=(?P<lovel>\d+)\s+"
    r"hivel=(?P<hivel>\d+)\s+"
    r"seq_position=(?P<seq>\d+)"
)
GROUP_KEY_RE = re.compile(r"\bkey=(\d+)\b")


@dataclass(frozen=True)
class SfzRegion:
    key: int
    sample: str
    lovel: int
    hivel: int
    seq_position: int


def parse_sfz_regions(sfz_path: Path) -> list[SfzRegion]:
    regions: list[SfzRegion] = []
    current_key: int | None = None

    for raw_line in sfz_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("//"):
            continue

        if line.startswith("<group>"):
            match = GROUP_KEY_RE.search(line)
            current_key = int(match.group(1)) if match else None
            continue

        if not line.startswith("<region>") or current_key is None:
            continue

        match = REGION_RE.search(line)
        if match is None:
            continue

        regions.append(
            SfzRegion(
                key=current_key,
                sample=match.group("sample"),
                lovel=int(match.group("lovel")),
                hivel=int(match.group("hivel")),
                seq_position=int(match.group("seq")),
            )
        )

    return regions


def mic_score(sample_name: str, preferences: tuple[str, ...]) -> int:
    lowered = sample_name.lower()
    for index, token in enumerate(preferences):
        if token in lowered:
            return index
    return len(preferences) + 1


def resolve_sample_path(samples_dir: Path, sample_name: str) -> Path | None:
    direct = samples_dir / sample_name
    if direct.is_file():
        return direct

    # SFZ occasionally references "tir" while files on disk use "dir".
    alternate = sample_name.replace(" tir ", " dir ")
    if alternate != sample_name:
        alt_path = samples_dir / alternate
        if alt_path.is_file():
            return alt_path

    return None


def pick_region(
    regions: list[SfzRegion],
    sfz_key: int,
    samples_dir: Path,
) -> tuple[SfzRegion, Path] | None:
    available: list[tuple[SfzRegion, Path]] = []
    for region in regions:
        if region.key != sfz_key or region.seq_position != 1:
            continue
        resolved = resolve_sample_path(samples_dir, region.sample)
        if resolved is not None:
            available.append((region, resolved))

    if not available:
        return None

    preferences = MIC_PREFERENCES.get(sfz_key, ())
    target_velocity = 64

    def sort_key(item: tuple[SfzRegion, Path]) -> tuple[int, int, int]:
        region, _ = item
        velocity_mid = (region.lovel + region.hivel) // 2
        return (
            mic_score(region.sample, preferences),
            abs(velocity_mid - target_velocity),
            region.lovel,
        )

    return min(available, key=sort_key)


def sanitize_filename(label: str, midi_note: int) -> str:
    safe = re.sub(r'[<>:"/\\|?*]', "_", label).replace(" ", "_")
    return f"{safe}_{midi_note}.wav"


def build_preset(
    *,
    kit_name: str,
    sfz_path: Path,
    samples_dir: Path,
    output_samples_dir: Path,
    gain: float,
) -> dict:
    regions = parse_sfz_regions(sfz_path)
    pads: list[dict] = []

    for pad in GM_PADS:
        entry: dict = {
            "label": pad["label"],
            "gain": gain,
            "midi_note": pad["midi_note"],
        }

        if pad["choke_group"] > 0:
            entry["choke_group"] = pad["choke_group"]

        sfz_key = pad["sfz_key"]
        if sfz_key is not None:
            picked = pick_region(regions, sfz_key, samples_dir)
            if picked is not None:
                region, source = picked
                dest_name = sanitize_filename(pad["label"], pad["midi_note"])
                dest = output_samples_dir / dest_name
                shutil.copy2(source, dest)
                entry["sample"] = f"SAMPLES/{dest_name}"

        pads.append(entry)

    return {"name": kit_name, "pads": pads}


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    default_preset_dir = repo_root / "shared/PRESETS"
    default_output_samples = repo_root / "shared/SAMPLES"

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sfz", type=Path, required=True, help="Path to SFZ instrument file")
    parser.add_argument("--samples-dir", type=Path, required=True, help="Directory containing SFZ sample WAVs")
    parser.add_argument("--preset-dir", type=Path, default=default_preset_dir)
    parser.add_argument("--output-samples-dir", type=Path, default=default_output_samples)
    parser.add_argument("--kit-name", default="Acoustic Drum Kit")
    parser.add_argument("--preset-name", default="acoustic-drum-kit")
    parser.add_argument("--gain", type=float, default=1.0)
    args = parser.parse_args()

    args.preset_dir.mkdir(parents=True, exist_ok=True)
    args.output_samples_dir.mkdir(parents=True, exist_ok=True)

    preset = build_preset(
        kit_name=args.kit_name,
        sfz_path=args.sfz,
        samples_dir=args.samples_dir,
        output_samples_dir=args.output_samples_dir,
        gain=args.gain,
    )

    preset_path = args.preset_dir / f"{args.preset_name}.json"
    preset_path.write_text(json.dumps(preset, indent=2) + "\n", encoding="utf-8")

    assigned = sum(1 for pad in preset["pads"] if "sample" in pad)
    print(f"Wrote preset: {preset_path}")
    print(f"Copied {assigned}/{len(preset['pads'])} pad samples to {args.output_samples_dir}")


if __name__ == "__main__":
    main()
