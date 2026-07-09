# Arch Linux EFI image builder

Builds a bootable x86_64 Arch Linux GPT/EFI disk image that auto-starts `Rythmbox.App` in fullscreen kiosk mode.

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
- `FASTBOOT` - `1` hides boot logs and shows Plymouth splash, default `1`
- `DEBUG_BOOT` - `1` enables verbose console/serial boot logs and disables Plymouth

## Boot behavior

- EFI boots with GRUB installed as removable media (`EFI/BOOT/BOOTX64.EFI`).
- **Fastboot** (`FASTBOOT=1`, default) shows a Plymouth splash with `scripts/arch-efi/splash.png` instead of kernel boot logs.
- Set `DEBUG_BOOT=1` to disable Plymouth and keep serial/console boot logs for troubleshooting.
- TTY1 auto-logins as `rythmbox`.
- `startx` launches `/opt/rhythmbox/app/Rythmbox.App`.
- `RYTHMBOX_FULLSCREEN=1` starts the Avalonia window fullscreen.

## Notes

The image installs NetworkManager and OpenSSH for remote maintenance. Linux shutdown/restart from the app uses `systemctl poweroff` and `systemctl reboot`; on the kiosk image the app user has passwordless sudo for maintenance tasks, but the app's systemctl calls normally work through logind/polkit in a local session.
