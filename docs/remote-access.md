# Running PianoMapper Remotely (archie via laptop)

PianoMapper runs entirely on archie (headless Hyprland session, no physical monitor). View and hear it from the laptop over SSH — no ports exposed beyond SSH itself.

## Every session: run these two on the laptop

**Video:**
```bash
ssh -f -N -L 5900:localhost:5900 mirusser@192.168.0.74   # or 100.114.200.74 over Tailscale
vncviewer localhost:5900
```

**Audio** (leave running while you work):
```bash
ssh mirusser@192.168.0.74 "parec -d @DEFAULT_MONITOR@ --format=s16le --rate=48000 --channels=2 --latency-msec=20" | ffmpeg -fflags nobuffer -flush_packets 1 -f s16le -ar 48000 -ac 2 -i - -f wav - | SDL_AUDIODRIVER=pulseaudio SDL_AUDIO_SAMPLES=512 ffplay -analyzeduration 0 -probesize 32 -nodisp -loglevel quiet -
```

Same commands work whether you're on the LAN or on Tailscale — just swap the host IP.

## Gotchas already hit, so you don't rediscover them

- `@DEFAULT_MONITOR@` is literal syntax (not a placeholder) — `parec` resolves it itself to "the monitor of whatever's playing."
- `ffplay` needs `SDL_AUDIODRIVER=pulseaudio` forced explicitly, or it picks a backend that produces no sound even though it "plays" successfully.
- Passing raw-format flags (`-f s16le -ar ... -ac ...`) straight to `ffplay` on a live stdin stream fails ("Option not found") on this ffmpeg version — route through `ffmpeg` first to wrap it as a self-describing WAV stream, then `ffplay` needs no format flags at all.
- Paste multi-line commands as a single line — broken line-continuations (`\`) have caused stray tokens to execute as their own command.
- Without `--latency-msec`, `parec` lets PulseAudio/pipewire-pulse pick its own (non-realtime-oriented, multi-second) buffer target — noticeable as a multi-second lag between playing a note and hearing it. `--latency-msec=20` + `ffmpeg -fflags nobuffer` tightens this substantially. If it's still laggy, try lowering it further (e.g. `--latency-msec=10`) or set `SDL_AUDIO_SAMPLES=512` before `ffplay` to shrink its output buffer too.
- `ffplay`'s default `-analyzeduration`/`-probesize` (5s / 5MB) makes it sit and "analyze" a piped, length-unknown stream before it starts playing anything at all — parec/ffmpeg keep streaming for real the whole time, so ffplay starts ~5s behind and *stays* that far behind for the rest of the session (a fixed offset, not a growing one — restarting the pipe just re-incurs the same 5s). Fixed by `-analyzeduration 0 -probesize 32`, since the format (WAV/PCM/48kHz/stereo) is already fully known — there's nothing to analyze.
- After the above fixes, remaining latency (~0.5s) is basically the floor for this pipe shape (SSH -> ffmpeg -> ffplay -> SDL -> PulseAudio, a few ms per hop). `ffmpeg -flush_packets 1` and `SDL_AUDIO_SAMPLES=512` shave a bit more off; past that it's diminishing returns without a purpose-built network-audio protocol.

## One-time setup (already done on archie, for reference)

- `wayvnc` installed, bound to `127.0.0.1:5900` only (`~/.config/wayvnc/config`) — reachable exclusively through the SSH tunnel above, no separate VNC password.
- Runs as a systemd user service (`~/.config/systemd/user/wayvnc.service`), auto-restarts.
- Hyprland has no physical monitor, so a virtual `headless` output is created via `hyprctl output create headless`; this is persisted through an `exec-once` line in `~/.config/hypr/hyprland.conf` so it survives reboots.
