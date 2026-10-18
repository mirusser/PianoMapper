using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PianoMapper.Web;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Importing;
using PianoMapper.Web.Playback;
using PianoMapper.Web.Practice;
using PianoMapper.Web.Scores;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Recognizing a score image can outlast HttpClient's default 100 second timeout (the optional alternate recognizer takes
// minutes); the server bounds every recognition run with its own timeout, so the importer's client waits for it.
builder.Services.AddScoped(_ => new ScoreFileImporter(new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = Timeout.InfiniteTimeSpan,
}));
builder.Services.AddScoped<WebAudioSession>();
builder.Services.AddScoped<IBrowserScoreAudio>(services => services.GetRequiredService<WebAudioSession>());
builder.Services.AddScoped<IBrowserMetronomeAudio>(services => services.GetRequiredService<WebAudioSession>());
builder.Services.AddScoped<BrowserScorePlayback>();
builder.Services.AddScoped<BrowserMetronome>();
builder.Services.AddScoped<ExerciseClick>();
builder.Services.AddScoped<SavedScoreClient>();
builder.Services.AddScoped<IProgressCache, BrowserProgressCache>();
builder.Services.AddScoped<IProgressServer, ProgressServerClient>();
builder.Services.AddScoped<ProgressStore>();

await builder.Build().RunAsync();
