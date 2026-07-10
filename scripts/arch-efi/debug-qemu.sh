#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

QEMU_BIN_DIR="${QEMU_BIN_DIR:-${REPO_ROOT}/bin}"
UEFI_DIR="${UEFI_DIR:-${REPO_ROOT}/bin/share}"
IMAGE_PATH="${IMAGE_PATH:-${REPO_ROOT}/out/rythmbox-arch-efi.img}"
QEMU_WORK_DIR="${QEMU_WORK_DIR:-${REPO_ROOT}/out/qemu-debug}"
MEMORY="${MEMORY:-2048}"
CPUS="${CPUS:-4}"
SSH_PORT="${SSH_PORT:-2222}"
MONITOR_PORT="${MONITOR_PORT:-4444}"
SERIAL_PORT="${SERIAL_PORT:-5555}"
SERIAL_TELNET="${SERIAL_TELNET:-1}"
NO_SERIAL_TCP="${NO_SERIAL_TCP:-0}"
ALLOW_REBOOT="${ALLOW_REBOOT:-0}"
QEMU_TRACE="${QEMU_TRACE:-guest_errors,unimp}"
QEMU_ACCEL="${QEMU_ACCEL:-kvm:tcg}"
QEMU_DISPLAY="${QEMU_DISPLAY:-gtk,gl=off}"
DISPLAY_WIDTH="${DISPLAY_WIDTH:-1600}"
DISPLAY_HEIGHT="${DISPLAY_HEIGHT:-900}"
ALLOW_WINDOWS_QEMU="${ALLOW_WINDOWS_QEMU:-0}"
AUTO_INSTALL="${AUTO_INSTALL:-0}"
CHECK_ONLY="${CHECK_ONLY:-0}"

for arg in "$@"; do
  case "${arg}" in
    --check)
      CHECK_ONLY=1
      ;;
  esac
done

resolve_full_path() {
  local path="$1"
  if [[ "${path}" != /* ]]; then
    path="${PWD}/${path}"
  fi
  if [[ -e "${path}" ]]; then
    readlink -f "${path}"
  else
    printf '%s\n' "${path}"
  fi
}

pacman_pkg_installed() {
  pacman -Q "$1" >/dev/null 2>&1
}

ovmf_code_candidates() {
  printf '%s\n' \
    "/usr/share/edk2/x64/OVMF_CODE.fd" \
    "/usr/share/edk2/x64/OVMF_CODE.4m.fd" \
    "/usr/share/edk2/x64/OVMF_CODE.secboot.4m.fd" \
    "/usr/share/OVMF/OVMF_CODE.fd" \
    "/usr/share/OVMF/OVMF_CODE_4M.fd" \
    "/usr/share/qemu/OVMF_CODE.fd" \
    "${UEFI_DIR}/OVMF_CODE.fd" \
    "${UEFI_DIR}/OVMF_CODE_4M.fd" \
    "${UEFI_DIR}/edk2-x86_64-code.fd"
}

ovmf_vars_candidates() {
  printf '%s\n' \
    "/usr/share/edk2/x64/OVMF_VARS.fd" \
    "/usr/share/edk2/x64/OVMF_VARS.4m.fd" \
    "/usr/share/OVMF/OVMF_VARS.fd" \
    "/usr/share/OVMF/OVMF_VARS_4M.fd" \
    "/usr/share/qemu/OVMF_VARS.fd" \
    "${UEFI_DIR}/OVMF_VARS.fd" \
    "${UEFI_DIR}/OVMF_VARS_4M.fd" \
    "${UEFI_DIR}/edk2-x86_64-vars.fd"
}

ovmf_code_available() {
  if [[ -n "${OVMF_CODE:-}" && -f "${OVMF_CODE}" ]]; then
    return 0
  fi
  local candidate
  while IFS= read -r candidate; do
    if [[ -f "${candidate}" ]]; then
      return 0
    fi
  done < <(ovmf_code_candidates)
  return 1
}

ensure_pacman_debug_deps() {
  if ! command -v pacman >/dev/null 2>&1; then
    return 0
  fi

  local missing_pkgs=()
  local qemu_pkg=""

  if [[ -z "${QEMU:-}" ]] && ! command -v qemu-system-x86_64 >/dev/null 2>&1; then
    if ! pacman_pkg_installed qemu-system-x86 \
      && ! pacman_pkg_installed qemu-full \
      && ! pacman_pkg_installed qemu-desktop; then
      missing_pkgs+=(qemu-system-x86)
      qemu_pkg="qemu-system-x86"
    fi
  fi

  if ! ovmf_code_available && ! pacman_pkg_installed edk2-ovmf; then
    missing_pkgs+=(edk2-ovmf)
  fi

  if [[ "${#missing_pkgs[@]}" -eq 0 ]]; then
    return 0
  fi

  echo "Missing host packages for QEMU debug: ${missing_pkgs[*]}" >&2
  if [[ "${AUTO_INSTALL}" == "1" ]]; then
    if [[ "${EUID}" -ne 0 ]]; then
      sudo pacman -Sy --needed --noconfirm "${missing_pkgs[@]}"
    else
      pacman -Sy --needed --noconfirm "${missing_pkgs[@]}"
    fi
    return 0
  fi

  echo "Install with: sudo pacman -S --needed ${missing_pkgs[*]}" >&2
  echo "Or rerun with AUTO_INSTALL=1 to install automatically." >&2
  if [[ -n "${qemu_pkg}" ]]; then
    echo "Or set QEMU=/path/to/qemu-system-x86_64." >&2
  fi
  exit 1
}

find_first() {
  local candidate
  for candidate in "$@"; do
    if [[ -f "${candidate}" ]]; then
      resolve_full_path "${candidate}"
      return 0
    fi
  done
  return 1
}

ensure_pacman_debug_deps

QEMU="${QEMU:-}"
if [[ -z "${QEMU}" ]]; then
  QEMU="$(command -v qemu-system-x86_64 || true)"
fi

if [[ -z "${QEMU}" ]]; then
  QEMU="$(find_first "${QEMU_BIN_DIR}/qemu-system-x86_64" || true)"
fi

if [[ -z "${QEMU}" && "${ALLOW_WINDOWS_QEMU}" == "1" ]]; then
  QEMU="$(find_first \
    "${QEMU_BIN_DIR}/qemu-system-x86_64.exe" \
    "${QEMU_BIN_DIR}/qemu-system-x86_64w.exe" \
    || true)"
fi

if [[ -z "${QEMU}" ]]; then
  echo "native qemu-system-x86_64 not found." >&2
  echo "Install qemu for Linux, or set QEMU=/path/to/qemu-system-x86_64." >&2
  echo "To intentionally use Windows QEMU from WSL, set ALLOW_WINDOWS_QEMU=1." >&2
  exit 1
fi

if [[ "${QEMU}" == *.exe && "${ALLOW_WINDOWS_QEMU}" != "1" ]]; then
  echo "Refusing to use Windows QEMU from Linux host: ${QEMU}" >&2
  echo "Install native Linux qemu-system-x86_64, or set ALLOW_WINDOWS_QEMU=1 if this is intentional." >&2
  exit 1
fi

QEMU="$(resolve_full_path "${QEMU}")"
QEMU_BIN_DIR="$(resolve_full_path "${QEMU_BIN_DIR}")"
UEFI_DIR="$(resolve_full_path "${UEFI_DIR}")"
IMAGE_PATH="$(resolve_full_path "${IMAGE_PATH}")"
QEMU_WORK_DIR="$(resolve_full_path "${QEMU_WORK_DIR}")"

if [[ ! -f "${IMAGE_PATH}" ]]; then
  echo "Image not found: ${IMAGE_PATH}. Set IMAGE_PATH." >&2
  exit 1
fi

OVMF_CODE="${OVMF_CODE:-}"
if [[ -z "${OVMF_CODE}" ]]; then
  mapfile -t _ovmf_code_candidates < <(ovmf_code_candidates)
  OVMF_CODE="$(find_first "${_ovmf_code_candidates[@]}" || true)"
fi

OVMF_VARS="${OVMF_VARS:-}"
if [[ -z "${OVMF_VARS}" ]]; then
  mapfile -t _ovmf_vars_candidates < <(ovmf_vars_candidates)
  OVMF_VARS="$(find_first "${_ovmf_vars_candidates[@]}" || true)"
fi

if [[ -z "${OVMF_CODE}" ]]; then
  echo "UEFI code firmware not found in ${UEFI_DIR}." >&2
  echo "Expected OVMF_CODE.fd, OVMF_CODE_4M.fd, or edk2-x86_64-code.fd. Set OVMF_CODE." >&2
  if command -v pacman >/dev/null 2>&1; then
    echo "On Arch Linux install: sudo pacman -S --needed edk2-ovmf" >&2
  fi
  exit 1
fi

if [[ "${CHECK_ONLY}" == "1" ]]; then
  cat <<CHECK
QEMU debug dependencies OK
  QEMU:       ${QEMU}
  UEFI code:  ${OVMF_CODE}
  UEFI vars:  ${OVMF_VARS:-none}
  Image:      ${IMAGE_PATH}
CHECK
  exit 0
fi

OVMF_CODE="$(resolve_full_path "${OVMF_CODE}")"
if [[ -n "${OVMF_VARS}" ]]; then
  OVMF_VARS="$(resolve_full_path "${OVMF_VARS}")"
fi

mkdir -p "${QEMU_WORK_DIR}"

VARS_DRIVE=()
OVMF_VARS_COPY=""
if [[ -n "${OVMF_VARS}" ]]; then
  OVMF_VARS_COPY="${QEMU_WORK_DIR}/OVMF_VARS.fd"
  if [[ ! -f "${OVMF_VARS_COPY}" || "${OVMF_VARS}" -nt "${OVMF_VARS_COPY}" ]]; then
    cp "${OVMF_VARS}" "${OVMF_VARS_COPY}"
  fi
  VARS_DRIVE=(-drive "if=pflash,format=raw,file=${OVMF_VARS_COPY}")
else
  echo "Warning: UEFI vars firmware not found; booting without persistent NVRAM vars." >&2
fi

SERIAL_LOG="${QEMU_WORK_DIR}/serial.log"
DEBUGCON_LOG="${QEMU_WORK_DIR}/debugcon.log"
QEMU_LOG="${QEMU_WORK_DIR}/qemu.log"
MONITOR_ADDR="127.0.0.1:${MONITOR_PORT}"
SERIAL_ADDR="127.0.0.1:${SERIAL_PORT}"

if [[ "${NO_SERIAL_TCP}" == "1" ]]; then
  SERIAL_CHARDEV="file,id=serial0,path=${SERIAL_LOG}"
  SERIAL_TCP_LABEL="disabled"
else
  TELNET_FLAG=""
  if [[ "${SERIAL_TELNET}" == "1" ]]; then
    TELNET_FLAG=",telnet=on"
  fi
  SERIAL_CHARDEV="socket,id=serial0,host=127.0.0.1,port=${SERIAL_PORT},server=on,wait=off${TELNET_FLAG},logfile=${SERIAL_LOG},logappend=on"
  SERIAL_TCP_LABEL="telnet 127.0.0.1 ${SERIAL_PORT}"
fi

cat <<INFO
QEMU debug boot
  QEMU:       ${QEMU}
  UEFI code:  ${OVMF_CODE}
  UEFI vars:  ${OVMF_VARS:-none}
  Image:      ${IMAGE_PATH}
  Memory:     ${MEMORY} MiB
  CPUs:       ${CPUS}
  Display:    ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT}
  SSH:        localhost:${SSH_PORT} -> guest:22
  Serial log: ${SERIAL_LOG}
  Debugcon:   ${DEBUGCON_LOG}
  QEMU log:   ${QEMU_LOG}
  Serial TCP: ${SERIAL_TCP_LABEL}
  Monitor:    telnet 127.0.0.1 ${MONITOR_PORT}

Inside guest after boot:
  ssh rythmbox@localhost -p ${SSH_PORT}

Serial console/debug:
  tail -f "${SERIAL_LOG}"
  telnet 127.0.0.1 ${SERIAL_PORT}
INFO

QEMU_ARGS=(
  -name rythmbox-arch-efi-debug
  -machine "q35,accel=${QEMU_ACCEL}"
  -m "${MEMORY}"
  -smp "${CPUS}"
  -rtc base=utc
  -D "${QEMU_LOG}"
  -d "${QEMU_TRACE}"
  -drive "if=pflash,format=raw,readonly=on,file=${OVMF_CODE}"
  "${VARS_DRIVE[@]}"
  -drive "file=${IMAGE_PATH},if=virtio,format=raw,cache=writeback"
  -netdev "user,id=net0,hostfwd=tcp::${SSH_PORT}-:22"
  -device virtio-net-pci,netdev=net0
  -device "virtio-vga,xres=${DISPLAY_WIDTH},yres=${DISPLAY_HEIGHT}"
  -display "${QEMU_DISPLAY}"
  -device intel-hda
  -device hda-duplex
  -chardev "${SERIAL_CHARDEV}"
  -serial chardev:serial0
  -debugcon "file:${DEBUGCON_LOG}"
  -global isa-debugcon.iobase=0x402
  -monitor "tcp:${MONITOR_ADDR},server,nowait"
)

if [[ "${ALLOW_REBOOT}" != "1" ]]; then
  QEMU_ARGS+=(-no-reboot)
fi

exec "${QEMU}" "${QEMU_ARGS[@]}"
