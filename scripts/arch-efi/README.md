# Arch Linux EFI image builder

Builds a bootable x86_64 Arch Linux GPT/EFI disk image that auto-starts the embedded host `Rythmbox.Shell` fullscreen on **DRM/KMS** — no X server, no Wayland compositor. It renders directly to the framebuffer with libinput for touch and mouse input.

## Host setup

Run on a Linux build host:

```bash
sudo scripts/arch-efi/setup-host.sh
```

## Build image

```bash
sudo scripts/arch-efi/build-image.sh
```

Default bootstrap:

```text
https://mirror.kku.ac.th/archlinux/iso/2026.07.01/archlinux-bootstrap-2026.07.01-x86_64.tar.zst
```

Default output:

```text
build/arch-efi/rythmbox-arch-efi.img
```

## Useful overrides

```bash
sudo IMAGE_SIZE=12G IMAGE_NAME=rythmbox.img scripts/arch-efi/build-image.sh
```

Variables:

- `BOOTSTRAP_URL` - Arch bootstrap tarball URL
- `ARCH_MIRROR` - pacman mirror URL
- `IMAGE_NAME` - output image filename
- `IMAGE_SIZE` - raw disk image size, default `8G`
- `WORK_DIR` - work/output directory, default `build/arch-efi`
- `HOSTNAME` - image hostname, default `rythmbox`
- `KIOSK_USER` - autologin user, default `rythmbox`
- `ROOT_PASSWORD` - root password in the image, default `rythmbox`
- `USER_PASSWORD` - kiosk user password, default `rythmbox`
- `FASTBOOT` - `1` firmware-style silent boot: no GRUB menu, no boot text, Plymouth splash only, default `1`
- `DEBUG_BOOT` - `1` enables verbose console/serial boot logs, a GRUB menu, and disables Plymouth

## Boot behavior

Firmware-style silent boot — the display shows only the splash, then the app. No GRUB menu, kernel log, or login prompt appears on screen.

- EFI boots with GRUB installed as removable media (`EFI/BOOT/BOOTX64.EFI`); the menu is hidden with a 0s timeout (`FASTBOOT=1`).
- **Fastboot** (default) shows a Plymouth splash (`scripts/arch-efi/splash.png`); kernel/systemd text is diverted to tty3 (**Alt+F3** for diagnostics), not the primary display.
- Plymouth is held for the whole boot and handed off to the app with `--retain-splash`, so the splash stays frozen on screen until the first app frame replaces it — no flicker to a console.
- TTY1 auto-logins as `rythmbox` (no prompt, no banner) and launches `/opt/rhythmbox/start-kiosk.sh`.
- `start-kiosk.sh` runs `Rythmbox.Shell`, the embedded host that renders on DRM/KMS with libinput for **touch and mouse** input.
- Set `DEBUG_BOOT=1` to disable Plymouth and keep serial/console boot logs + a visible GRUB menu for troubleshooting.

## Embedded host (`Rythmbox.Shell`)

The kiosk runs a dedicated executable, `src/Rythmbox.Shell`, separate from the desktop `Rythmbox.App`:

- Renders the Rythmbox UI straight to the framebuffer via **DRM/KMS + EGL** — no X11/Wayland.
- Drives input through **libinput**, which delivers both **touchscreen** and **mouse/keyboard** events.
- Draws its own **software mouse cursor** (the framebuffer backend has no hardware/compositor pointer). The cursor follows a physical mouse and stays hidden for touch, so a touchscreen UI is uncluttered.
- Runs on Linux only; on any other OS it prints a message and exits. Desktop platforms use `Rythmbox.App`.

### DRM/KMS overrides

The embedded host honors these environment variables (see `start-kiosk.sh`):

- `RYTHMBOX_DRM_CARD` - DRM device node; unset by default so Avalonia auto-detects the primary connected output (override with e.g. `/dev/dri/card1`).
- `RYTHMBOX_DRM_SCALE` - output scaling factor, default `1.0`.
- `RYTHMBOX_DRM_ROTATION` - screen rotation in degrees: `0`, `90`, `180`, `270` (default `0`), for portrait/rotated panels.

## No X11 / Wayland

The image ships **no X server, no xinit/startx, and no display manager** — login and rendering are DRM/KMS-only. The build also strips any X server/session packages that could be pulled in transitively. Note that mesa's `libx11`/`libxcb` *client* libraries remain (they are hard mesa dependencies needed for GPU/EGL rendering) but are inert with no X server present.

## Notes

The image installs NetworkManager and OpenSSH for remote maintenance. Linux shutdown/restart from the app uses `systemctl poweroff` and `systemctl reboot`; on the kiosk image the app user has passwordless sudo for maintenance tasks, but the app's systemctl calls normally work through logind/polkit in a local session.

After `dotnet publish`, the build removes the source tree, .NET SDK/runtime packages, git, pacman package cache, NuGet caches, and non-English locales. The published self-contained app under `/opt/rhythmbox/app` is kept, along with `shared/` presets copied beside it. A `4G` image is usually enough after optimization; the default `8G` leaves headroom for growth.
