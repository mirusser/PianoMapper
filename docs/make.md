# Running PianoMapper with `make`

`make` is the recommended way to run the complete browser app: it starts PostgreSQL, makes sure Audiveris (image recognition) is available, and starts the hosted app. Run every command from the repository root.

## Targets

| Command | What it does | Needs Docker |
| --- | --- | --- |
| `make` (same as `make run`) | Starts PostgreSQL and Audiveris, then runs the app with `dotnet run` in Development. The browser client runs on the .NET WebAssembly interpreter. | Yes, unless `ConnectionStrings__PianoMapper` is set |
| `make run-release` | The same prerequisites, then publishes the browser client in Release with WebAssembly AOT and serves it from the published output. | Yes, unless `ConnectionStrings__PianoMapper` is set |
| `make publish-release` | Only publishes the AOT build to `PianoMapper.Server/bin/Release/net10.0/publish-aot/`. Does not start anything. | No |

`make` is the default goal, so a bare `make` runs `make run`.

## Prerequisites

- **Docker** with the Compose plugin, running (`sudo systemctl start docker`). If Docker is not running, `make` and `make run-release` print that command and exit with code 2 before doing anything else. Setting `ConnectionStrings__PianoMapper` skips the Docker check and the PostgreSQL start entirely.
- **.NET SDK 10** on your `PATH` for `make`.
- **Audiveris**: an `audiveris` command on `PATH` is used if present. Otherwise, on Linux x86_64, the first run downloads Audiveris 5.10.2 (about 68 MiB, checksum-verified) into `.tools/` and reuses it afterwards; that needs `curl`, `ar`, `sha256sum`, `tar` and `unzstd`. On other platforms install Audiveris yourself and set `Omr__AudiverisExecutable`.
- **For `make run-release` and `make publish-release` only**: Linux x86_64 plus `curl`, `gzip`, `sha512sum` and `tar`.

## What `make` does

1. Checks that Docker is running (skipped when `ConnectionStrings__PianoMapper` is set).
2. Starts the `postgres` service from `compose.yaml` (`docker compose up --detach --wait postgres`) and waits until it is healthy. The database listens on `127.0.0.1:5434`, and its data lives in the `pianomapper-postgres-data` Docker volume, so it survives restarts.
3. Finds Audiveris, or installs the repo-local copy, and exports `Omr__AudiverisExecutable`.
4. Runs `dotnet run --project PianoMapper.Server/PianoMapper.Server.csproj`. The launch profile serves the app at `http://localhost:5080` in Development and opens a browser.

Then open the app, select **Enable audio**, and connect your MIDI keyboard.

## What `make run-release` adds

`make run-release` does steps 1 to 3 above, then runs `scripts/publish-aot.sh --run`:

1. On first use, downloads a pinned .NET SDK 10.0.112 (about 230 MiB, checksum-verified) and the `wasm-tools` workload (about 250 MiB) into `.tools/`. About 1.7 GiB on disk. It exists because AOT needs the workload, and a root-owned system SDK cannot install it without `sudo`. Your `dotnet` on `PATH` is never touched.
2. Publishes with `-p:RunAOTCompilation=true` into `PianoMapper.Server/bin/Release/net10.0/publish-aot/`, keeping its intermediates in `obj/aot/`. The first publish takes a few minutes; an unchanged relaunch takes seconds.
3. Starts the published server in Production from that directory, on `http://localhost:5080`.

Trade-offs: the browser downloads more (about 5.8 MiB compressed instead of 2.8 MiB for a plain Release publish) and every code change recompiles. After rebuilding, **close every PianoMapper tab and reopen it**; the offline service worker keeps serving the previous build to a plain reload.

## Environment variables

Set them on the same line as `make`, for example `Omr__AudiverisExecutable=/opt/audiveris/bin/Audiveris make`.

| Variable | Effect | Default |
| --- | --- | --- |
| `ConnectionStrings__PianoMapper` | Use this PostgreSQL connection instead of the compose one. Skips the Docker check and `docker compose up`. | `make`: from `appsettings.Development.json`. `make run-release`: the compose database on port 5434. |
| `Omr__AudiverisExecutable` | Path to the Audiveris launcher. | `audiveris` on `PATH`, else `.tools/audiveris-5.10.2/opt/audiveris/bin/Audiveris` |
| `Omr__OemerExecutable` | Path to the optional alternate recognizer (oemer). Off when unset. See [Alternate image recognition](../README.md#alternate-image-recognition-optional). | `.tools/oemer/venv/bin/oemer` when `scripts/install-oemer.sh` has created it, otherwise unset |
| `Omr__OemerTimeoutSeconds` | How long one alternate-recognizer run may take before it is stopped. | `600` |
| `ASPNETCORE_URLS` | Address of the published server. Only `make run-release` reads it. | `http://localhost:5080` |

Arguments are not forwarded by `make`. To pass extra arguments to the app, call the script directly: `./scripts/run-web.sh <args>` forwards them to `dotnet run`, and `./scripts/publish-aot.sh --run <args>` forwards them to the published server.

## Common tasks

- **Stop the app**: `Ctrl+C` in the terminal running `make`.
- **Stop PostgreSQL**: `docker compose down`. The data volume is kept. `docker compose down --volumes` also deletes the stored progress and scores.
- **Free the AOT toolchain**: `rm -rf .tools/dotnet-sdk-10.0.112`. It is downloaded again on the next `make run-release`.
- **Remove the AOT build output**: delete `PianoMapper.Server/bin/Release/net10.0/publish-aot/` and the `obj/aot/` folder inside each project (about 230 MiB of intermediates).
- **Run without Docker**: point `ConnectionStrings__PianoMapper` at any PostgreSQL you have, then `make`.

## Related

- [README quick start](../README.md#-quick-start) and [AOT-compiled release build](../README.md#aot-compiled-release-build).
- [Remote access](remote-access.md) for running the app on another machine.
