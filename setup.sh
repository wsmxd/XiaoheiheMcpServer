#!/usr/bin/env bash
set -euo pipefail

DOTNET_CHANNEL="LTS"
PLAYWRIGHT_BROWSERS="chromium"
PLAYWRIGHT_SCRIPT_PATH=""
MIN_CHROMIUM_VERSION="120"
FORCE_DOTNET_INSTALL="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dotnet-channel)
      DOTNET_CHANNEL="${2:?missing value for --dotnet-channel}"
      shift 2
      ;;
    --playwright-browsers)
      PLAYWRIGHT_BROWSERS="${2:?missing value for --playwright-browsers}"
      shift 2
      ;;
    --playwright-script-path)
      PLAYWRIGHT_SCRIPT_PATH="${2:?missing value for --playwright-script-path}"
      shift 2
      ;;
    --minimum-chromium-major-version)
      MIN_CHROMIUM_VERSION="${2:?missing value for --minimum-chromium-major-version}"
      shift 2
      ;;
    --force-dotnet-install)
      FORCE_DOTNET_INSTALL="true"
      shift
      ;;
    -h|--help)
      echo "Usage: ./setup.sh [--dotnet-channel LTS] [--playwright-browsers chromium] [--minimum-chromium-major-version 120] [--force-dotnet-install]"
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OS_NAME="$(uname -s)"

log_step() {
  printf '[SETUP] %s\n' "$1"
}

log_ok() {
  printf '[ OK ] %s\n' "$1"
}

log_warn() {
  printf '[WARN] %s\n' "$1"
}

prepend_path() {
  local dir="$1"
  [[ -d "$dir" ]] || return 0
  case ":$PATH:" in
    *":$dir:"*) ;;
    *) export PATH="$dir:$PATH" ;;
  esac
}

default_playwright_browsers_path() {
  if [[ -n "${PLAYWRIGHT_BROWSERS_PATH:-}" && "${PLAYWRIGHT_BROWSERS_PATH:-}" != "0" ]]; then
    printf '%s\n' "$PLAYWRIGHT_BROWSERS_PATH"
    return
  fi

  if [[ "$OS_NAME" == "Darwin" ]]; then
    printf '%s\n' "$HOME/Library/Caches/ms-playwright"
  else
    printf '%s\n' "$HOME/.cache/ms-playwright"
  fi
}

chromium_major_version() {
  local executable="$1"
  [[ -x "$executable" ]] || return 1

  local version_text
  version_text="$($executable --version 2>/dev/null || true)"
  if [[ "$version_text" =~ ([0-9]{2,3})\.[0-9]+\. ]]; then
    printf '%s\n' "${BASH_REMATCH[1]}"
    return 0
  fi

  return 1
}

is_usable_chromium() {
  local executable="$1"
  local major

  major="$(chromium_major_version "$executable" || true)"
  [[ -n "$major" ]] || return 1
  [[ "$major" -ge "$MIN_CHROMIUM_VERSION" ]]
}

add_if_exists() {
  local executable="$1"
  [[ -f "$executable" ]] || return 0
  CHROMIUM_CANDIDATES+=("$executable")
}

collect_chromium_candidates() {
  CHROMIUM_CANDIDATES=()

  if [[ -n "${XIAOHEIHE_CHROMIUM_PATH:-}" ]]; then
    add_if_exists "$XIAOHEIHE_CHROMIUM_PATH"
  fi

  local browsers_root
  browsers_root="$(default_playwright_browsers_path)"
  if [[ -d "$browsers_root" ]]; then
    while IFS= read -r -d '' dir; do
      if [[ "$OS_NAME" == "Darwin" ]]; then
        add_if_exists "$dir/chrome-mac/Chromium.app/Contents/MacOS/Chromium"
      else
        add_if_exists "$dir/chrome-linux/chrome"
      fi
    done < <(find "$browsers_root" -maxdepth 1 -type d -name 'chromium-*' -print0 2>/dev/null)
  fi

  if [[ "$OS_NAME" == "Darwin" ]]; then
    add_if_exists "/Applications/Chromium.app/Contents/MacOS/Chromium"
    add_if_exists "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    add_if_exists "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
    add_if_exists "$HOME/Applications/Chromium.app/Contents/MacOS/Chromium"
    add_if_exists "$HOME/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    add_if_exists "$HOME/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
  else
    add_if_exists "/usr/bin/chromium"
    add_if_exists "/usr/bin/chromium-browser"
    add_if_exists "/usr/bin/google-chrome"
    add_if_exists "/usr/bin/google-chrome-stable"
    add_if_exists "/usr/bin/microsoft-edge"
    add_if_exists "/usr/bin/microsoft-edge-stable"
    add_if_exists "/snap/bin/chromium"
  fi

  local command_name command_path
  for command_name in chromium chromium-browser google-chrome google-chrome-stable chrome microsoft-edge microsoft-edge-stable; do
    command_path="$(command -v "$command_name" 2>/dev/null || true)"
    [[ -n "$command_path" ]] && add_if_exists "$command_path"
  done
}

has_usable_chromium() {
  collect_chromium_candidates

  local candidate
  for candidate in "${CHROMIUM_CANDIDATES[@]:-}"; do
    if is_usable_chromium "$candidate"; then
      log_ok "检测到可用 Chromium/Chrome：$candidate"
      return 0
    fi
  done

  return 1
}

resolve_playwright_script() {
  if [[ -n "$PLAYWRIGHT_SCRIPT_PATH" ]]; then
    printf '%s\n' "$PLAYWRIGHT_SCRIPT_PATH"
    return
  fi

  local candidates=(
    "$SCRIPT_DIR/playwright.ps1"
    "$SCRIPT_DIR/XiaoheiheMcpServer/bin/Debug/net10.0/playwright.ps1"
    "$SCRIPT_DIR/XiaoheiheMcpServer/bin/Release/net10.0/playwright.ps1"
    "$SCRIPT_DIR/XiaoheiheMcpServer/bin/Release/net10.0/publish/playwright.ps1"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  done

  return 1
}

log_step "开始 macOS/Linux 环境配置"

if [[ "$OS_NAME" != "Darwin" && "$OS_NAME" != "Linux" ]]; then
  echo "setup.sh only supports macOS and Linux. Use setup.ps1 or install-chromium.bat on Windows."
  exit 1
fi

DOTNET_DIR="$HOME/.dotnet"
DOTNET_TOOLS_DIR="$HOME/.dotnet/tools"
prepend_path "$DOTNET_DIR"
prepend_path "$DOTNET_TOOLS_DIR"

if command -v dotnet >/dev/null 2>&1 && [[ "$FORCE_DOTNET_INSTALL" != "true" ]]; then
  log_ok ".NET 已检测到：$(dotnet --version)"
else
  log_step "正在安装/更新 .NET SDK（Channel=$DOTNET_CHANNEL，InstallDir=$DOTNET_DIR）"
  DOTNET_INSTALL_SCRIPT="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_INSTALL_SCRIPT"
  chmod +x "$DOTNET_INSTALL_SCRIPT"
  "$DOTNET_INSTALL_SCRIPT" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR"
  rm -f "$DOTNET_INSTALL_SCRIPT"
  prepend_path "$DOTNET_DIR"
  prepend_path "$DOTNET_TOOLS_DIR"
  log_ok ".NET 安装完成：$(dotnet --version)"
fi

log_step "dotnet --info（用于确认环境）"
dotnet --info

need_install="false"
for browser in $PLAYWRIGHT_BROWSERS; do
  if [[ "$browser" == "chromium" ]] && has_usable_chromium; then
    log_ok "检测到主版本 >= $MIN_CHROMIUM_VERSION 的 Chromium/Chrome，跳过安装"
  else
    need_install="true"
  fi
done

if [[ "$need_install" == "true" ]]; then
  if ! command -v pwsh >/dev/null 2>&1; then
    echo "pwsh not found. Please install PowerShell 7+ and rerun this script."
    exit 1
  fi

  PLAYWRIGHT_SCRIPT="$(resolve_playwright_script || true)"
  if [[ -z "$PLAYWRIGHT_SCRIPT" || ! -f "$PLAYWRIGHT_SCRIPT" ]]; then
    echo "playwright.ps1 not found. Run dotnet build first or pass --playwright-script-path."
    exit 1
  fi

  log_step "使用脚本安装 Playwright 浏览器：$PLAYWRIGHT_BROWSERS（脚本：$PLAYWRIGHT_SCRIPT）"
  pwsh -NoProfile -ExecutionPolicy Bypass -File "$PLAYWRIGHT_SCRIPT" install $PLAYWRIGHT_BROWSERS
  log_ok "Playwright 浏览器安装完成"
fi

log_ok "环境配置完成。"
