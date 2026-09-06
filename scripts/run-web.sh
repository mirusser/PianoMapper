#!/usr/bin/env bash
set -euo pipefail

readonly audiveris_version="5.10.2"
readonly audiveris_package="Audiveris-${audiveris_version}-ubuntu24.04-x86_64.deb"
readonly audiveris_url="https://github.com/Audiveris/audiveris/releases/download/${audiveris_version}/${audiveris_package}"
readonly audiveris_sha256="cadb9fafc0be228718c2fbef2e802e04787241150398714251b00c51e31b5198"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repo_root
readonly tools_directory="${repo_root}/.tools"
readonly audiveris_directory="${tools_directory}/audiveris-${audiveris_version}"
readonly local_audiveris="${audiveris_directory}/opt/audiveris/bin/Audiveris"

require_command() {
    local command_name="$1"
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Cannot install Audiveris: '${command_name}' is required." >&2
        exit 1
    fi
}

install_audiveris() {
    if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
        echo "Automatic Audiveris installation supports Linux x86_64 only." >&2
        echo "Install Audiveris manually and set Omr__AudiverisExecutable to its launcher path." >&2
        exit 1
    fi

    require_command ar
    require_command curl
    require_command sha256sum
    require_command tar
    require_command unzstd

    mkdir -p "${tools_directory}"
    installer_temporary_directory="$(mktemp -d "${tools_directory}/.audiveris-install.XXXXXX")"
    trap cleanup_installer EXIT

    local package_path="${installer_temporary_directory}/${audiveris_package}"
    echo "Installing Audiveris ${audiveris_version} (one-time download, about 68 MiB)..."
    curl --fail --location --show-error --output "${package_path}" "${audiveris_url}"
    printf '%s  %s\n' "${audiveris_sha256}" "${package_path}" | sha256sum --check

    (
        cd "${installer_temporary_directory}"
        ar x "${package_path}" data.tar.zst
    )
    mkdir -p "${audiveris_directory}"
    tar --extract \
        --use-compress-program=unzstd \
        --file "${installer_temporary_directory}/data.tar.zst" \
        --directory "${audiveris_directory}"

    if [[ ! -x "${local_audiveris}" ]]; then
        echo "Audiveris installation did not contain the expected launcher." >&2
        exit 1
    fi

    echo "Audiveris installed at ${local_audiveris}."
    cleanup_installer
    trap - EXIT
}

cleanup_installer() {
    if [[ -n "${installer_temporary_directory:-}" && -d "${installer_temporary_directory}" ]]; then
        rm -rf -- "${installer_temporary_directory}"
    fi
}

cd "${repo_root}"

audiveris_executable="${Omr__AudiverisExecutable:-}"
if [[ -z "${audiveris_executable}" ]]; then
    if command -v audiveris >/dev/null 2>&1; then
        audiveris_executable="audiveris"
    else
        if [[ ! -x "${local_audiveris}" ]]; then
            install_audiveris
        fi
        audiveris_executable="${local_audiveris}"
    fi
fi

if ! command -v "${audiveris_executable}" >/dev/null 2>&1; then
    echo "Could not find Audiveris at '${audiveris_executable}'." >&2
    exit 1
fi

export Omr__AudiverisExecutable="${audiveris_executable}"
exec dotnet run --project PianoMapper.Server/PianoMapper.Server.csproj "$@"
