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

cleanup() {
  set +e
  if mountpoint -q "${MOUNT_DIR}/proc"; then umount -R "${MOUNT_DIR}/proc"; fi
  if mountpoint -q "${MOUNT_DIR}/sys"; then umount -R "${MOUNT_DIR}/sys"; fi
  if mountpoint -q "${MOUNT_DIR}/dev"; then umount -R "${MOUNT_DIR}/dev"; fi
  if mountpoint -q "${MOUNT_DIR}/run"; then umount -R "${MOUNT_DIR}/run"; fi
  if mountpoint -q "${MOUNT_DIR}/boot"; then umount -R "${MOUNT_DIR}/boot"; fi
  if mountpoint -q "${MOUNT_DIR}"; then umount -R "${MOUNT_DIR}"; fi
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
write_file "${MOUNT_DIR}/etc/fstab" "UUID=${ROOT_UUID} / ext4 rw,relatime 0 1
UUID=${BOOT_UUID} /boot vfat rw,relatime,fmask=0022,dmask=0022,codepage=437,iocharset=ascii,shortname=mixed,utf8,errors=remount-ro 0 2
"

echo "Installing Arch packages"
chroot_run "pacman-key --init"
chroot_run "pacman-key --populate archlinux"
chroot_run "pacman -Syu --noconfirm"
chroot_run "pacman -S --needed --noconfirm base linux linux-firmware grub efibootmgr networkmanager sudo openssh git dotnet-sdk dotnet-runtime aspnet-runtime xorg-server xorg-xinit xorg-xrandr mesa libglvnd libx11 libice libsm fontconfig ttf-dejavu alsa-utils pipewire pipewire-alsa pipewire-pulse wireplumber dbus"

chroot_run "ln -sf /usr/share/zoneinfo/UTC /etc/localtime && hwclock --systohc || true"
chroot_run "locale-gen"
chroot_run "printf 'root:%s\\n' '${ROOT_PASSWORD}' | chpasswd"
chroot_run "id -u '${KIOSK_USER}' >/dev/null 2>&1 || useradd -m -G audio,video,input,wheel -s /bin/bash '${KIOSK_USER}'"
chroot_run "printf '%s:%s\\n' '${KIOSK_USER}' '${USER_PASSWORD}' | chpasswd"
write_file "${MOUNT_DIR}/etc/sudoers.d/99-${KIOSK_USER}" "${KIOSK_USER} ALL=(ALL) NOPASSWD: ALL
"
chmod 0440 "${MOUNT_DIR}/etc/sudoers.d/99-${KIOSK_USER}"

mkdir -p "${MOUNT_DIR}/opt/rhythmbox/source"
rsync -a --delete \
  --exclude .git \
  --exclude build \
  --exclude 'src/**/bin' \
  --exclude 'src/**/obj' \
  "${REPO_ROOT}/" "${MOUNT_DIR}/opt/rhythmbox/source/"

chroot_run "cd /opt/rhythmbox/source && dotnet restore Rythmbox.slnx"
chroot_run "cd /opt/rhythmbox/source && dotnet publish src/Rythmbox.App/Rythmbox.App.csproj -c Release -r linux-x64 --self-contained true -o /opt/rhythmbox/app"
chroot_run "chown -R '${KIOSK_USER}:${KIOSK_USER}' /opt/rhythmbox"

write_file "${MOUNT_DIR}/home/${KIOSK_USER}/.xinitrc" "#!/bin/sh
export RYTHMBOX_FULLSCREEN=1
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
xset -dpms
xset s off
exec /opt/rhythmbox/app/Rythmbox.App
"
chmod 0755 "${MOUNT_DIR}/home/${KIOSK_USER}/.xinitrc"
chroot_run "chown '${KIOSK_USER}:${KIOSK_USER}' '/home/${KIOSK_USER}/.xinitrc'"

mkdir -p "${MOUNT_DIR}/etc/systemd/system/getty@tty1.service.d"
write_file "${MOUNT_DIR}/etc/systemd/system/getty@tty1.service.d/autologin.conf" "[Service]
ExecStart=
ExecStart=-/usr/bin/agetty --autologin ${KIOSK_USER} --noclear %I \$TERM
"

cat > "${MOUNT_DIR}/home/${KIOSK_USER}/.bash_profile" <<'PROFILE'
if [[ -z "${DISPLAY}" ]] && [[ "$(tty)" == /dev/tty1 ]]; then
  exec startx
fi
PROFILE
chroot_run "chown '${KIOSK_USER}:${KIOSK_USER}' '/home/${KIOSK_USER}/.bash_profile'"

write_file "${MOUNT_DIR}/etc/default/grub" "GRUB_DEFAULT=0
GRUB_TIMEOUT=3
GRUB_DISTRIBUTOR=\"Rythmbox\"
GRUB_CMDLINE_LINUX_DEFAULT=\"console=tty0 console=ttyS0,115200n8 earlyprintk=serial,ttyS0,115200 loglevel=7 systemd.log_level=debug systemd.log_target=console\"
GRUB_CMDLINE_LINUX=\"\"
GRUB_TERMINAL_INPUT=\"console serial\"
GRUB_TERMINAL_OUTPUT=\"console serial\"
GRUB_SERIAL_COMMAND=\"serial --unit=0 --speed=115200 --word=8 --parity=no --stop=1\"
"

chroot_run "systemctl enable NetworkManager sshd systemd-timesyncd serial-getty@ttyS0 getty@tty1"
chroot_run "grub-install --target=x86_64-efi --efi-directory=/boot --bootloader-id=Rythmbox --removable --no-nvram"
chroot_run "grub-mkconfig -o /boot/grub/grub.cfg"

echo "Arch EFI image complete: ${IMAGE_PATH}"
