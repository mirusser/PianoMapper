#!/usr/bin/env bash
# Publishes PianoMapper.Server with the browser client compiled ahead of time to WebAssembly (Mono AOT), and with
# --run starts the server from that published output.
#
# AOT needs the `wasm-tools` workload (Emscripten). The system .NET SDK is usually root-owned, so this script
# provisions a repo-local SDK plus that workload under .tools/ on first use and runs only the AOT publish with it.
# Plain `dotnet build`, `dotnet test` and `make` keep using the system SDK and never need the workload.
set -euo pipefail

# Pinned SDK. To bump it, change the version and take the linux-x64 SDK archive's sha512 from
# https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json.
readonly dotnet_sdk_version="10.0.112"
readonly dotnet_sdk_archive="dotnet-sdk-${dotnet_sdk_version}-linux-x64.tar.gz"
readonly dotnet_sdk_url="https://builds.dotnet.microsoft.com/dotnet/Sdk/${dotnet_sdk_version}/${dotnet_sdk_archive}"
readonly dotnet_sdk_sha512="1ac949dfd3929855e3a102a5b12f843967a784ad2d911dfcfff6abdf81a2e78bccf25ddc055547de02b36133bf792aaf97fcba8db8aa6b52ac2e018b86935d05"
readonly workload_id="wasm-tools"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repo_root
readonly tools_directory="${repo_root}/.tools"
readonly dotnet_root="${tools_directory}/dotnet-sdk-${dotnet_sdk_version}"
readonly local_dotnet="${dotnet_root}/dotnet"
readonly publish_directory="${repo_root}/PianoMapper.Server/bin/Release/net10.0/publish-aot"

# The same development database compose.yaml starts and appsettings.Development.json names. A published app runs in
# Production, which does not read appsettings.Development.json.
readonly development_connection_string="Host=localhost;Port=5434;Database=pianomapper;Username=pianomapper;Password=pianomapper"

require_command() {
    local command_name="$1"
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Cannot publish PianoMapper with AOT: '${command_name}' is required." >&2
        exit 1
    fi
}

install_dotnet_sdk() {
    if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
        echo "Automatic .NET SDK installation supports Linux x86_64 only." >&2
        echo "Install the workload yourself ('sudo dotnet workload install ${workload_id}'), then run:" >&2
        echo "  dotnet publish PianoMapper.Server/PianoMapper.Server.csproj --configuration Release -p:IntermediateOutputPath=obj/aot/ -p:RunAOTCompilation=true" >&2
        exit 1
    fi

    require_command curl
    require_command gzip
    require_command sha512sum
    require_command tar

    mkdir -p "${tools_directory}"
    installer_temporary_directory="$(mktemp -d "${tools_directory}/.dotnet-install.XXXXXX")"
    trap cleanup_installer EXIT

    local archive_path="${installer_temporary_directory}/${dotnet_sdk_archive}"
    echo "Installing .NET SDK ${dotnet_sdk_version} under .tools/ (one-time download, about 230 MiB)..."
    curl --fail --location --show-error --output "${archive_path}" "${dotnet_sdk_url}"
    printf '%s  %s\n' "${dotnet_sdk_sha512}" "${archive_path}" | sha512sum --check

    mkdir "${installer_temporary_directory}/sdk"
    tar --extract --gzip --file "${archive_path}" --directory "${installer_temporary_directory}/sdk"
    rm -- "${archive_path}"
    mv -- "${installer_temporary_directory}/sdk" "${dotnet_root}"

    if [[ ! -x "${local_dotnet}" ]]; then
        echo "The .NET SDK archive did not contain the expected dotnet launcher." >&2
        exit 1
    fi

    echo ".NET SDK installed at ${dotnet_root}."
    cleanup_installer
    trap - EXIT
}

install_workload() {
    mkdir -p "${tools_directory}"
    installer_temporary_directory="$(mktemp -d "${tools_directory}/.dotnet-install.XXXXXX")"
    trap cleanup_installer EXIT

    # The SDK's own workload manifests pin the exact pack versions that match this SDK, so nothing else is looked up.
    echo "Installing the ${workload_id} workload (one-time download, about 250 MiB)..."
    "${local_dotnet}" workload install "${workload_id}" \
        --skip-manifest-update \
        --temp-dir "${installer_temporary_directory}"

    cleanup_installer
    trap - EXIT
}

cleanup_installer() {
    if [[ -n "${installer_temporary_directory:-}" && -d "${installer_temporary_directory}" ]]; then
        rm -rf -- "${installer_temporary_directory}"
    fi
}

publish_with_aot() {
    # Fingerprinted framework files from earlier publishes would otherwise pile up in the output directory.
    rm -rf -- "${publish_directory}"

    echo "Publishing PianoMapper with WebAssembly AOT compilation (Release)."
    echo "The first publish takes a few minutes; later publishes reuse unchanged compiled output."
    # AOT strips the IL out of the intermediate assemblies, which a later plain `dotnet publish --configuration Release`
    # would reuse and then fail to start in the browser, so the AOT build keeps its own obj/aot/ intermediates.
    "${local_dotnet}" publish "${repo_root}/PianoMapper.Server/PianoMapper.Server.csproj" \
        --configuration Release \
        --output "${publish_directory}" \
        -p:IntermediateOutputPath=obj/aot/ \
        -p:RunAOTCompilation=true
    echo "Published to ${publish_directory}."
}

run_published_server() {
    export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5080}"
    export ConnectionStrings__PianoMapper="${ConnectionStrings__PianoMapper:-${development_connection_string}}"

    echo "Starting the published app on ${ASPNETCORE_URLS}."
    echo "If the browser still shows an older build, close every PianoMapper tab and reopen it:"
    echo "the offline service worker only switches to a new build once no tab uses the old one."

    cd "${publish_directory}"
    exec "${local_dotnet}" PianoMapper.Server.dll "$@"
}

run_server=false
if [[ "${1:-}" == "--run" ]]; then
    run_server=true
    shift
fi

cd "${repo_root}"

if [[ ! -x "${local_dotnet}" ]]; then
    install_dotnet_sdk
fi

# Use the repo-local SDK for everything below, including the MSBuild node processes it spawns.
export DOTNET_ROOT="${dotnet_root}"
export PATH="${dotnet_root}:${PATH}"
export DOTNET_NOLOGO=1

if ! "${local_dotnet}" workload list | grep --quiet --word-regexp "${workload_id}"; then
    install_workload
fi

publish_with_aot

if [[ "${run_server}" == true ]]; then
    run_published_server "$@"
fi
