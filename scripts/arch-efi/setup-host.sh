#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run as root: sudo scripts/arch-efi/setup-host.sh" >&2
  exit 1
fi

if command -v apt-get >/dev/null 2>&1; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update
  apt-get install -y \
    ca-certificates \
    curl \
    dosfstools \
    e2fsprogs \
    gdisk \
    git \
    parted \
    qemu-utils \
    rsync \
    tar \
    util-linux \
    wget \
    xz-utils \
    zstd
elif command -v pacman >/dev/null 2>&1; then
  pacman -Sy --needed --noconfirm \
    arch-install-scripts \
    ca-certificates \
    curl \
    dosfstools \
    e2fsprogs \
    git \
    gptfdisk \
    parted \
    qemu-img \
    rsync \
    tar \
    wget \
    xz \
    zstd
else
  echo "Unsupported host package manager. Install: curl dosfstools e2fsprogs git parted qemu-utils rsync tar util-linux wget xz zstd" >&2
  exit 1
fi

echo "Host image build dependencies installed."
