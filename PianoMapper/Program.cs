using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using PianoMapper;
using PianoMapper.Music;

Score? loadedScore = ScoreCommandLine.Load(args, Console.Error);

// GameWindowSettings.Default leaves UpdateFrequency uncapped (0 = unlimited), which lets
// OnUpdateFrame fire many times per actual input poll - a single keypress then reads as
// "just pressed" on every one of those calls, firing a burst of overlapping notes instead of one.
var gameWindowSettings = new GameWindowSettings
{
    UpdateFrequency = 60.0,
};

var nativeWindowSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1024, 600),
    Title = "PianoMapper",
};

using var window = new PianoMapperWindow(gameWindowSettings, nativeWindowSettings, loadedScore);
window.Run();
