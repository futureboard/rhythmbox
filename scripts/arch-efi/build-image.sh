#!/usr/bin/env bash
set -euo pipefail

BOOTSTRAP_URL="${BOOTSTRAP_URL:-https://mirror.kku.ac.th/archlinux/iso/2026.07.01/archlinux-bootstrap-2026.07.01-x86_64.tar.zst}"
ARCH_MIRROR="${ARCH_MIRROR:-https://mirror.kku.ac.th/archlinux/\$repo/os/\$arch}"
IMAGE_NAME="${IMAGE_NAME:-rythmbox-arch-efi.img}"
IMAGE_SIZE="${IMAGE_SIZE:-8G}"
BOOT_SIZE_MIB="${BOOT_SIZE_MIB:-512}"
WORK_DIR="${WORK_DIR:-build/arch-efi}"
HOSTNAME="${HOSTNAME:-rythmbox}"
KIOSK_USER="${KIOSK_USER:-rythmbox}"
ROOT_PASSWORD="${ROOT_PASSWORD:-rythmbox}"
USER_PASSWORD="${USER_PASSWORD:-rythmbox}"
FASTBOOT="${FASTBOOT:-1}"
DEBUG_BOOT="${DEBUG_BOOT:-0}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK_DIR="${REPO_ROOT}/${WORK_DIR}"
DOWNLOAD_DIR="${WORK_DIR}/downloads"
ROOTFS_DIR="${WORK_DIR}/rootfs"
MOUNT_DIR="${WORK_DIR}/mnt"
IMAGE_PATH="${WORK_DIR}/${IMAGE_NAME}"
BOOTSTRAP_ARCHIVE="${DOWNLOAD_DIR}/$(basename "${BOOTSTRAP_URL}")"
LOOP_DEVICE=""

require_root() {
  if [[ "${EUID}" -ne 0 ]]; then
    echo "Run as root: sudo scripts/arch-efi/build-image.sh" >&2
    exit 1
  fi
}

ensure_loop_devices() {
  if [[ ! -e /dev/loop-control ]]; then
    mknod /dev/loop-control c 10 237
  fi

  for index in {0..15}; do
    if [[ ! -e "/dev/loop${index}" ]]; then
      mknod "/dev/loop${index}" b 7 "${index}"
    fi
  done
}

require_commands() {
  local missing=0
  for cmd in blkid curl losetup mkfs.ext4 mkfs.vfat parted rsync tar truncate zstd; do
    if ! command -v "${cmd}" >/dev/null 2>&1; then
      echo "Missing required command: ${cmd}" >&2
      missing=1
    fi
  done
  if [[ "${missing}" -ne 0 ]]; then
    echo "Install host dependencies first: sudo scripts/arch-efi/setup-host.sh" >&2
    exit 1
  fi
}

# Kill any process still rooted inside the target rootfs. pacman-key --init leaves
# gpg-agent/dirmngr running, and those keep the bind-mounted /dev busy, which is the
# usual cause of "umount: target is busy".
kill_chroot_processes() {
  [[ -d "${MOUNT_DIR}" ]] || return 0
  local mnt pid root
  mnt="$(readlink -f "${MOUNT_DIR}" 2>/dev/null)" || return 0
  [[ -n "${mnt}" ]] || return 0
  for pid in /proc/[0-9]*; do
    root="$(readlink "${pid}/root" 2>/dev/null)" || continue
    if [[ "${root}" == "${mnt}" || "${root}" == "${mnt}/"* ]]; then
      kill -TERM "${pid##*/}" 2>/dev/null || true
    fi
  done
  sleep 1
  for pid in /proc/[0-9]*; do
    root="$(readlink "${pid}/root" 2>/dev/null)" || continue
    if [[ "${root}" == "${mnt}" || "${root}" == "${mnt}/"* ]]; then
      kill -KILL "${pid##*/}" 2>/dev/null || true
    fi
  done
}

# Unmount a target, retrying then falling back to a lazy detach so cleanup never wedges.
unmount_target() {
  local target="$1"
  mountpoint -q "${target}" || return 0
  umount -R "${target}" 2>/dev/null && return 0
  sleep 1
  umount -R "${target}" 2>/dev/null && return 0
  umount -R -l "${target}" 2>/dev/null || true
}

cleanup() {
  set +e
  kill_chroot_processes
  # Deepest mounts first; /dev and /run are bind mounts of host dirs.
  unmount_target "${MOUNT_DIR}/dev"
  unmount_target "${MOUNT_DIR}/proc"
  unmount_target "${MOUNT_DIR}/sys"
  unmount_target "${MOUNT_DIR}/run"
  unmount_target "${MOUNT_DIR}/boot"
  unmount_target "${MOUNT_DIR}"
  if [[ -n "${LOOP_DEVICE}" ]]; then losetup -d "${LOOP_DEVICE}" >/dev/null 2>&1 || true; fi
}
trap cleanup EXIT

chroot_run() {
  chroot "${MOUNT_DIR}" /usr/bin/env -i \
    HOME=/root \
    TERM="${TERM:-xterm}" \
    PATH=/usr/local/sbin:/usr/local/bin:/usr/bin \
    /bin/bash -lc "$1"
}

write_file() {
  local target="$1"
  local content="$2"
  install -d "$(dirname "${target}")"
  printf "%s" "${content}" > "${target}"
}

install_plymouth_theme() {
  local splash_src="${REPO_ROOT}/scripts/arch-efi/splash.png"
  local theme_src="${REPO_ROOT}/scripts/arch-efi/plymouth"
  local theme_dir="${MOUNT_DIR}/usr/share/plymouth/themes/rythmbox"

  if [[ ! -f "${splash_src}" ]]; then
    echo "Missing splash image: ${splash_src}" >&2
    exit 1
  fi

  mkdir -p "${theme_dir}"
  install -m 0644 "${splash_src}" "${theme_dir}/splash.png"
  install -m 0644 "${theme_src}/rythmbox.plymouth" "${theme_dir}/rythmbox.plymouth"
  install -m 0644 "${theme_src}/rythmbox.script" "${theme_dir}/rythmbox.script"

  write_file "${MOUNT_DIR}/etc/plymouth/plymouthd.conf" "[Daemon]
Theme=rythmbox
ShowDelay=0
DeviceTimeout=8
"
}

configure_plymouth_initramfs() {
  chroot_run "grep -q plymouth /etc/mkinitcpio.conf || sed -i 's/^HOOKS=(base udev /HOOKS=(base udev plymouth /' /etc/mkinitcpio.conf"
  chroot_run "plymouth-set-default-theme -R rythmbox"
}

optimize_image() {
  echo "Optimizing image: removing toolchain, source tree, and caches"

  if [[ -d "${REPO_ROOT}/shared" ]]; then
    rsync -a "${REPO_ROOT}/shared/" "${MOUNT_DIR}/opt/rhythmbox/app/shared/"
  elif [[ -d "${MOUNT_DIR}/opt/rhythmbox/source/shared" ]]; then
    rsync -a "${MOUNT_DIR}/opt/rhythmbox/source/shared/" "${MOUNT_DIR}/opt/rhythmbox/app/shared/"
  fi

  chroot_run "rm -rf \
    /opt/rhythmbox/source \
    /root/.nuget \
    /root/.dotnet \
    /root/.local/share/NuGet \
    /root/.cache \
    /tmp/* \
    /var/tmp/*"

  chroot_run "pacman -Rns --noconfirm \
    dotnet-sdk \
    dotnet-runtime \
    aspnet-runtime \
    git \
    efibootmgr \
    || true"

  # Guarantee an X-free image: strip any X server / session / utility that may have
  # been pulled in transitively. The kiosk logs in and renders on DRM/KMS only.
  # (mesa's libx11/libxcb *client* libs are hard mesa deps and stay, but are unused
  # with no X server present.)
  chroot_run "pacman -Rns --noconfirm xorg-server xorg-server-common xorg-xinit xorg-xrandr xorg-xauth xterm 2>/dev/null || true"

  chroot_run "pacman -Scc --noconfirm"
  chroot_run "rm -rf /var/cache/pacman/pkg/*"

  chroot_run "find /usr/share/man -type f -delete 2>/dev/null || true"
  chroot_run "find /usr/share/doc -type f -delete 2>/dev/null || true"
  chroot_run "find /usr/share/info -type f -delete 2>/dev/null || true"
  chroot_run "find /usr/share/locale -mindepth 1 -maxdepth 1 ! -name 'en_US*' -exec rm -rf {} + 2>/dev/null || true"
  chroot_run "find /opt/rhythmbox/app -name '*.pdb' -delete 2>/dev/null || true"

  chroot_run "chown -R '${KIOSK_USER}:${KIOSK_USER}' /opt/rhythmbox"
}

require_root
require_commands
ensure_loop_devices

mkdir -p "${DOWNLOAD_DIR}" "${ROOTFS_DIR}" "${MOUNT_DIR}"

if [[ ! -f "${BOOTSTRAP_ARCHIVE}" ]]; then
  echo "Downloading Arch bootstrap: ${BOOTSTRAP_URL}"
  curl -L "${BOOTSTRAP_URL}" -o "${BOOTSTRAP_ARCHIVE}"
fi

rm -rf "${ROOTFS_DIR}" "${MOUNT_DIR}"
mkdir -p "${ROOTFS_DIR}" "${MOUNT_DIR}"

echo "Extracting bootstrap"
tar --numeric-owner --xattrs --xattrs-include='*' -I zstd -xf "${BOOTSTRAP_ARCHIVE}" -C "${ROOTFS_DIR}"
BOOTSTRAP_ROOT="${ROOTFS_DIR}/root.x86_64"
if [[ ! -d "${BOOTSTRAP_ROOT}" ]]; then
  echo "Bootstrap root not found at ${BOOTSTRAP_ROOT}" >&2
  exit 1
fi

echo "Creating disk image ${IMAGE_PATH} (${IMAGE_SIZE})"
rm -f "${IMAGE_PATH}"
truncate -s "${IMAGE_SIZE}" "${IMAGE_PATH}"
parted -s "${IMAGE_PATH}" mklabel gpt
parted -s "${IMAGE_PATH}" mkpart ESP fat32 1MiB "$((BOOT_SIZE_MIB + 1))MiB"
parted -s "${IMAGE_PATH}" set 1 esp on
parted -s "${IMAGE_PATH}" mkpart ROOT ext4 "$((BOOT_SIZE_MIB + 1))MiB" 100%

LOOP_DEVICE="$(losetup --find --show --partscan "${IMAGE_PATH}")"
sleep 1
BOOT_PART="${LOOP_DEVICE}p1"
ROOT_PART="${LOOP_DEVICE}p2"
if [[ ! -b "${BOOT_PART}" ]]; then BOOT_PART="/dev/mapper/$(basename "${LOOP_DEVICE}")p1"; fi
if [[ ! -b "${ROOT_PART}" ]]; then ROOT_PART="/dev/mapper/$(basename "${LOOP_DEVICE}")p2"; fi

mkfs.vfat -F 32 -n RYTHMBOOT "${BOOT_PART}"
mkfs.ext4 -F -L RYTHMROOT "${ROOT_PART}"

mount "${ROOT_PART}" "${MOUNT_DIR}"
mkdir -p "${MOUNT_DIR}/boot"
mount "${BOOT_PART}" "${MOUNT_DIR}/boot"

rsync -aHAX "${BOOTSTRAP_ROOT}/" "${MOUNT_DIR}/"

mount --bind /dev "${MOUNT_DIR}/dev"
mount --bind /run "${MOUNT_DIR}/run"
mount -t proc proc "${MOUNT_DIR}/proc"
mount -t sysfs sys "${MOUNT_DIR}/sys"

write_file "${MOUNT_DIR}/etc/pacman.d/mirrorlist" "Server = ${ARCH_MIRROR}
"
write_file "${MOUNT_DIR}/etc/hostname" "${HOSTNAME}
"
write_file "${MOUNT_DIR}/etc/hosts" "127.0.0.1 localhost
::1 localhost
127.0.1.1 ${HOSTNAME}.localdomain ${HOSTNAME}
"
write_file "${MOUNT_DIR}/etc/locale.gen" "en_US.UTF-8 UTF-8
"
write_file "${MOUNT_DIR}/etc/vconsole.conf" "KEYMAP=us
"

ROOT_UUID="$(blkid -s UUID -o value "${ROOT_PART}")"
BOOT_UUID="$(blkid -s UUID -o value "${BOOT_PART}")"
write_file "${MOUNT_DIR}/etc/fstab" "UUID=${ROOT_UUID} / ext4 rw,noatime,commit=60 0 1
UUID=${BOOT_UUID} /boot vfat rw,noatime,fmask=0022,dmask=0022,codepage=437,iocharset=ascii,shortname=mixed,utf8,errors=remount-ro 0 2
"

echo "Installing Arch packages"
chroot_run "pacman-key --init"
chroot_run "pacman-key --populate archlinux"
chroot_run "pacman -Syu --noconfirm"
# The kiosk renders directly on DRM/KMS (no X11): mesa/libglvnd provide EGL/GLES/GBM,
# libdrm the KMS interface, libinput + libxkbcommon the input stack Avalonia uses.
chroot_run "pacman -S --needed --noconfirm base linux linux-firmware grub efibootmgr networkmanager sudo openssh git dotnet-sdk dotnet-runtime aspnet-runtime mesa libglvnd libdrm libinput libxkbcommon fontconfig ttf-dejavu alsa-utils pipewire pipewire-alsa pipewire-pulse wireplumber dbus plymouth"

chroot_run "ln -sf /usr/share/zoneinfo/UTC /etc/localtime && hwclock --systohc || true"
chroot_run "locale-gen"
chroot_run "printf 'root:%s\\n' '${ROOT_PASSWORD}' | chpasswd"
chroot_run "id -u '${KIOSK_USER}' >/dev/null 2>&1 || useradd -m -G audio,video,input,wheel -s /bin/bash '${KIOSK_USER}'"
chroot_run "printf '%s:%s\\n' '${KIOSK_USER}' '${USER_PASSWORD}' | chpasswd"
write_file "${MOUNT_DIR}/etc/sudoers.d/99-${KIOSK_USER}" "${KIOSK_USER} ALL=(ALL) NOPASSWD: ALL
"
chmod 0440 "${MOUNT_DIR}/etc/sudoers.d/99-${KIOSK_USER}"

mkdir -p "${MOUNT_DIR}/opt/rhythmbox/source"
# Copy only what dotnet publish needs. Exclude build output, installers and disk
# images (dev.vmdk, out/*.img, the image we are currently writing) so the source
# copy can never pull a multi-GB artifact into the image and fill the disk.
rsync -a --delete \
  --exclude '.git' \
  --exclude 'build' \
  --exclude 'out' \
  --exclude 'installer' \
  --exclude 'src/**/bin' \
  --exclude 'src/**/obj' \
  --exclude '*.img' \
  --exclude '*.iso' \
  --exclude '*.vmdk' \
  --exclude '*.vhdx' \
  --exclude '*.qcow2' \
  "${REPO_ROOT}/" "${MOUNT_DIR}/opt/rhythmbox/source/"

chroot_run "cd /opt/rhythmbox/source && dotnet restore Rythmbox.slnx"
chroot_run "cd /opt/rhythmbox/source && dotnet publish src/Rythmbox.App/Rythmbox.App.csproj -c Release -r linux-x64 --self-contained true -o /opt/rhythmbox/app"
chroot_run "chown -R '${KIOSK_USER}:${KIOSK_USER}' /opt/rhythmbox"

optimize_image

# DRM/KMS launcher: renders straight to the framebuffer, no X server. Plymouth owns
# the DRM device during boot, so it must release it before the app becomes DRM master.
write_file "${MOUNT_DIR}/opt/rhythmbox/start-kiosk.sh" "#!/bin/sh
export RYTHMBOX_DRM=1
# Leave RYTHMBOX_DRM_CARD unset so Avalonia auto-detects the primary connected
# output; set it (e.g. /dev/dri/card1) only if a device needs a specific node.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
sudo -n plymouth quit --retain-splash 2>/dev/null || plymouth quit 2>/dev/null || true
exec /opt/rhythmbox/app/Rythmbox.App --drm
"
chmod 0755 "${MOUNT_DIR}/opt/rhythmbox/start-kiosk.sh"
chroot_run "chown '${KIOSK_USER}:${KIOSK_USER}' '/opt/rhythmbox/start-kiosk.sh'"

mkdir -p "${MOUNT_DIR}/etc/systemd/system/getty@tty1.service.d"
# --autologin logs the kiosk user in with no prompt; --noclear keeps the retained
# Plymouth splash on tty1; --noissue/--nohostname suppress the banner text.
write_file "${MOUNT_DIR}/etc/systemd/system/getty@tty1.service.d/autologin.conf" "[Service]
ExecStart=
ExecStart=-/usr/bin/agetty --autologin ${KIOSK_USER} --noclear --noissue --nohostname %I \$TERM
TTYVTDisallocate=no
"

# Blank the login/MOTD text so nothing appears on the console before the app.
write_file "${MOUNT_DIR}/etc/issue" ""
write_file "${MOUNT_DIR}/etc/motd" ""

cat > "${MOUNT_DIR}/home/${KIOSK_USER}/.bash_profile" <<'PROFILE'
if [[ -z "${RYTHMBOX_KIOSK}" ]] && [[ "$(tty)" == /dev/tty1 ]]; then
  export RYTHMBOX_KIOSK=1
  exec /opt/rhythmbox/start-kiosk.sh
fi
PROFILE
chroot_run "chown '${KIOSK_USER}:${KIOSK_USER}' '/home/${KIOSK_USER}/.bash_profile'"

if [[ "${DEBUG_BOOT}" == "1" ]]; then
  # Debug: show the menu and full kernel/serial logging.
  GRUB_TIMEOUT_VALUE=3
  GRUB_TIMEOUT_STYLE_VALUE="menu"
  KERNEL_CMDLINE="console=tty0 console=ttyS0,115200n8 earlyprintk=serial,ttyS0,115200 loglevel=7 systemd.log_level=debug systemd.log_target=console"
  GRUB_TERMINAL_INPUT_VALUE="console serial"
  GRUB_TERMINAL_OUTPUT_VALUE="console serial"
  USE_PLYMOUTH=0
elif [[ "${FASTBOOT}" == "1" ]]; then
  # Firmware-style: no GRUB menu, no kernel/systemd text on the visible display.
  # Kernel + service logs go to tty3 (Alt+F3 for diagnostics) so tty1 stays clean
  # for the Plymouth splash, which is held until the app takes over the display.
  GRUB_TIMEOUT_VALUE=0
  GRUB_TIMEOUT_STYLE_VALUE="hidden"
  KERNEL_CMDLINE="console=tty3 quiet splash loglevel=0 rd.udev.log_level=0 udev.log_level=0 rd.systemd.show_status=false systemd.show_status=false vt.global_cursor_default=0 consoleblank=0 nowatchdog plymouth.ignore-serial-consoles fbcon=nodefer"
  GRUB_TERMINAL_INPUT_VALUE="console"
  GRUB_TERMINAL_OUTPUT_VALUE="console"
  USE_PLYMOUTH=1
else
  GRUB_TIMEOUT_VALUE=0
  GRUB_TIMEOUT_STYLE_VALUE="hidden"
  KERNEL_CMDLINE="console=tty3 quiet splash loglevel=0 rd.systemd.show_status=false systemd.show_status=false vt.global_cursor_default=0 consoleblank=0"
  GRUB_TERMINAL_INPUT_VALUE="console"
  GRUB_TERMINAL_OUTPUT_VALUE="console"
  USE_PLYMOUTH=1
fi

write_file "${MOUNT_DIR}/etc/default/grub" "GRUB_DEFAULT=0
GRUB_TIMEOUT=${GRUB_TIMEOUT_VALUE}
GRUB_TIMEOUT_STYLE=${GRUB_TIMEOUT_STYLE_VALUE}
GRUB_RECORDFAIL_TIMEOUT=0
GRUB_DISTRIBUTOR=\"Rythmbox\"
GRUB_CMDLINE_LINUX_DEFAULT=\"${KERNEL_CMDLINE}\"
GRUB_CMDLINE_LINUX=\"\"
GRUB_TERMINAL_INPUT=\"${GRUB_TERMINAL_INPUT_VALUE}\"
GRUB_TERMINAL_OUTPUT=\"${GRUB_TERMINAL_OUTPUT_VALUE}\"
GRUB_SERIAL_COMMAND=\"serial --unit=0 --speed=115200 --word=8 --parity=no --stop=1\"
GRUB_DISABLE_OS_PROBER=true
"

mkdir -p "${MOUNT_DIR}/etc/systemd/system.conf.d" "${MOUNT_DIR}/etc/systemd/journald.conf.d"
write_file "${MOUNT_DIR}/etc/systemd/system.conf.d/10-rythmbox-fastboot.conf" "[Manager]
DefaultTimeoutStartSec=10s
DefaultTimeoutStopSec=5s
"
write_file "${MOUNT_DIR}/etc/systemd/journald.conf.d/10-volatile.conf" "[Journal]
Storage=volatile
RuntimeMaxUse=16M
"

chroot_run "systemctl enable NetworkManager sshd systemd-timesyncd getty@tty1"
if [[ "${DEBUG_BOOT}" == "1" ]]; then
  chroot_run "systemctl enable serial-getty@ttyS0"
fi
chroot_run "systemctl mask NetworkManager-wait-online.service systemd-networkd-wait-online.service"
if [[ "${USE_PLYMOUTH}" == "1" ]]; then
  echo "Installing Plymouth splash theme"
  install_plymouth_theme
  configure_plymouth_initramfs
  # Keep the splash on screen through the whole boot: the default quit services
  # would tear it down at multi-user, briefly exposing the console. start-kiosk.sh
  # instead quits Plymouth with --retain-splash right before the app draws, so the
  # splash image stays frozen on the display until the first app frame replaces it.
  chroot_run "systemctl mask plymouth-quit.service plymouth-quit-wait.service"
else
  chroot_run "mkinitcpio -P"
fi
chroot_run "grub-install --target=x86_64-efi --efi-directory=/boot --bootloader-id=Rythmbox --removable --no-nvram"
chroot_run "grub-mkconfig -o /boot/grub/grub.cfg"

echo "Arch EFI image complete: ${IMAGE_PATH}"
