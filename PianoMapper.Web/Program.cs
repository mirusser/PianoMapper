using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PianoMapper.Web;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Importing;
using PianoMapper.Web.Playback;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ScoreFileImporter>();
builder.Services.AddScoped<WebAudioSession>();
builder.Services.AddScoped<IBrowserScoreAudio>(services => services.GetRequiredService<WebAudioSession>());
builder.Services.AddScoped<IBrowserMetronomeAudio>(services => services.GetRequiredService<WebAudioSession>());
builder.Services.AddScoped<BrowserScorePlayback>();
builder.Services.AddScoped<BrowserMetronome>();

await builder.Build().RunAsync();
