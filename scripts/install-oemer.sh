#!/usr/bin/env bash
# Installs the optional alternate score-image recognizer (oemer, https://github.com/BreezeWhite/oemer) into a
# repo-local Python 3.13 virtual environment, with the exact dependency pins from scripts/oemer-requirements.txt and
# its two ONNX models (about 105 MiB, checksum-verified). Nothing here is needed unless you want the alternate
# recognizer; make, dotnet build and dotnet test never use it.
#
# Usage: scripts/install-oemer.sh [venv-directory]      (default: .tools/oemer/venv)
# Needs uv (https://docs.astral.sh/uv/), which fetches Python 3.13 if it is missing. Then point the server at it:
#   Omr__OemerExecutable=<venv-directory>/bin/oemer
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repo_root
readonly venv_directory="${1:-${repo_root}/.tools/oemer/venv}"
readonly requirements_file="${repo_root}/scripts/oemer-requirements.txt"
readonly python_version="3.13"

if ! command -v uv >/dev/null 2>&1; then
    echo "Cannot install oemer: 'uv' is required (https://docs.astral.sh/uv/)." >&2
    exit 1
fi

echo "Creating the oemer environment in ${venv_directory} (Python ${python_version})..."
uv venv --python "${python_version}" "${venv_directory}"
uv pip install --python "${venv_directory}/bin/python" --no-deps --requirement "${requirements_file}"

echo "Downloading the oemer models (one-time, about 105 MiB)..."
"${venv_directory}/bin/python" -I - <<'PYTHON'
import hashlib
import os
import urllib.request

from oemer import MODULE_PATH
from oemer.ete import CHECKPOINTS_URL

# SHA-256 of the oemer 0.1.8 release models; oemer itself downloads them without any verification.
EXPECTED_SHA256 = {
    "1st_model.onnx": "37512e858731096439746f60b377c049f07055b4a23ec6eb9a178ce92cfba174",
    "2nd_model.onnx": "ed2e1a86ea75712ee6cdc740e96f7a36753543cf9bb980227c071c9256d9d82e",
}

for title, expected in EXPECTED_SHA256.items():
    directory = os.path.join(MODULE_PATH, "checkpoints", "unet_big" if title.startswith("1st") else "seg_net")
    target = os.path.join(directory, "model.onnx")
    if os.path.exists(target):
        continue
    temporary = target + ".part"
    urllib.request.urlretrieve(CHECKPOINTS_URL[title], temporary)
    with open(temporary, "rb") as downloaded:
        actual = hashlib.sha256(downloaded.read()).hexdigest()
    if actual != expected:
        os.remove(temporary)
        raise SystemExit(f"{title}: unexpected checksum {actual}")
    os.replace(temporary, target)
    print(f"{title}: ok")
PYTHON

echo "oemer installed. Enable it with:"
echo "  Omr__OemerExecutable=${venv_directory}/bin/oemer make"
