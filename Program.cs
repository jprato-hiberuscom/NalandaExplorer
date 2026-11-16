using NalandaExplorer.Components;
using NalandaExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<NalandaSession>();
builder.Services.AddScoped<NalandaExplorerState>();
builder.Services.AddHttpClient<NalandaAuthenticationService>(client =>
    ConfigureNalandaApiClient(client));
builder.Services.AddHttpClient<NalandaApiClient>(client =>
    ConfigureNalandaApiClient(client));
builder.Services.AddHttpClient<NalandaSoapClient>(client =>
    client.BaseAddress = new Uri(
        "https://api.nalandaglobal.com/ws/integration/services/obras/exportacion/"))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static void ConfigureNalandaApiClient(HttpClient client)
{
    client.BaseAddress = new Uri("https://api.nalandaglobal.com/api/");
}
