SCREEN 1
========

A screen capture app that displays your screen in a borderless window with
adjustable FPS, quality, pixelation, fake cursor, and a game mode with
5-second delay buffer.

REQUIREMENTS
------------
- Windows 10/11
- .NET Framework 4.8 (already on Windows 10 1903+)
- .NET SDK 6+ to build (https://dotnet.microsoft.com/download)

BUILD
-----
Run build.bat or:
  dotnet build -c Release

Output: bin\Release\net48\Screen1.exe

HOW TO USE
----------
1. Run Screen1.exe - black fullscreen window appears
2. QuickBar at top (only you see it, not screen capture)
3. Click expand arrow on QuickBar
4. Click Settings to open control panel
5. Click Start to begin capture

CONTROLS
--------
- Start/Stop: Begin or end capture
- Freeze (F6): Pause the display
- Cursor: Hide real cursor, draw fake one on capture (laggy on purpose)
- Game Mode: 5-second delay buffer, Z/X/C/V for 3-sec pause, random freezes
- FPS: Random interval between min and max
- Quality: JPEG compression 1-100%
- Pixelation: Downscale factor 1-64x
- Region: Crop area, use presets or Show overlay to drag/resize

GAME MODE
---------
- Everything appears 5 seconds delayed
- Press Z/X/C/V to pause capture for 3 seconds (erases those actions)
- Random 2-second freezes every 12-32 seconds
- Buffer takes 10 seconds to initially fill
