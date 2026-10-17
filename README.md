# WinMixer

Per-application volume control from a USB MIDI controller (like MIDI Mixer, but simpler) — Windows 10/11.

- Register a MIDI controller, then **LEARN** a knob/fader (volume) and a button (mute) for each strip
- Targets: **Master**, any app with an audio session, or **Active window** (acts on whichever app has focus)
- Logarithmic (dB) or linear volume curve, adjustable range
- Presets (save / load / delete, also from the tray menu)
- Stereo VU meters, OSD (choose screen + 9 positions + duration)
- Start minimized, minimize/close to tray, start with Windows
- Relative (endless encoder) mode, pitch-bend faders supported

## Portable build

`dist/WinMixer-portable/WinMixer.exe` is self-contained (no .NET install needed). Settings and presets are stored in a `data` folder next to the exe.

## Build from source

Requires the .NET 8 SDK.

```
cd src
dotnet run -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ../dist/WinMixer-portable
```

Note: if another program (e.g. MIDI Mixer) has the MIDI device open, Windows may refuse to share it — the controller shows as BUSY.
