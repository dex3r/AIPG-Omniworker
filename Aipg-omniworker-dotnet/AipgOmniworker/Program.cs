using AipgOmniworker.Components;
using AipgOmniworker.OmniController;
using Serilog;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BridgeConfigManager>();
builder.Services.AddSingleton<GridWorkerController>();
builder.Services.AddSingleton<TextWorkerConfigManager>();
builder.Services.AddSingleton<AphroditeController>();
builder.Services.AddSingleton<OmniControllerMain>();
builder.Services.AddSingleton<ImageWorkerConfigManager>();
builder.Services.AddSingleton<ImageWorkerController>();
builder.Services.AddSingleton<PersistentStorage>();
builder.Services.AddSingleton<UserConfigManager>();

builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console()
    .WriteTo.File("/persistent/logs/omniworker.log", 
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 100000000) // 100 MB
    .CreateLogger();

builder.Services.AddSerilog();

// Expose to any address on port 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
