using AipgOmniworker;
using AipgOmniworker.Components;
using AipgOmniworker.OmniController;
using Serilog;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InstancesManager>();
builder.Services.AddSingleton<BridgeConfigManager>();
builder.Services.AddSingleton<TextWorkerConfigManager>();
builder.Services.AddSingleton<ImageWorkerConfigManager>();
builder.Services.AddSingleton<PersistentStorage>();
builder.Services.AddSingleton<UserConfigManager>();
builder.Services.AddSingleton<InstancesConfigManager>();

builder.Services.AddScoped<Instance>();
builder.Services.AddScoped<OmniControllerMain>();
builder.Services.AddScoped<ImageWorkerController>();
builder.Services.AddScoped<AphroditeController>();
builder.Services.AddScoped<GridWorkerController>();

builder.Logging.ClearProviders();

string consoleLogOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}][{SourceContext}] {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: consoleLogOutputTemplate)
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

InstancesConfigManager instancesConfigManager = app.Services.GetRequiredService<InstancesConfigManager>();
InstancesManager instancesManager = app.Services.GetRequiredService<InstancesManager>();
CancellationTokenSource appClosingToken = new();
List<Task> workersControllersTasks = new();

foreach (InstanceConfig instanceConfig in await instancesConfigManager.GetAllInstances())
{
    Instance instance = await instancesManager.GetInstance(instanceConfig.InstanceId);
    
    Task workersControllerTask = Task.Run(async () => await instance.OmniControllerMain.OnAppStarted(appClosingToken.Token),
        appClosingToken.Token);
    
    workersControllersTasks.Add(workersControllerTask);
}

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

appClosingToken.Cancel();

await Task.WhenAll(workersControllersTasks.ToArray());
