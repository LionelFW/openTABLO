using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Data;
using OpenTablo.Api.Endpoints;
using OpenTablo.Api.Geocoding;
using OpenTablo.Api.Infrastructure;
using OpenTablo.Api.Options;
using OpenTablo.Api.Prim;
using OpenTablo.Api.Referential;
using OpenTablo.Api.Velib;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PrimOptions>()
    .BindConfiguration(PrimOptions.Section)
    // PRIM_API_KEY (environment variable or user secret) is the documented way to provide the key.
    .Configure<IConfiguration>((o, config) => o.ApiKey = config["PRIM_API_KEY"] is { Length: > 0 } key ? key : o.ApiKey)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SearchOptions>().BindConfiguration(SearchOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OpenDataOptions>().BindConfiguration(OpenDataOptions.Section).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddDbContextFactory<TabloDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Tablo") ?? throw new InvalidOperationException("ConnectionStrings:Tablo is not configured."),
    npgsql => npgsql.UseNetTopologySuite()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHybridCache();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UpstreamExceptionHandler>();
builder.Services.AddHealthChecks();

// No automatic retries on PRIM: a retry would spend quota.
builder.Services.AddHttpClient<PrimClient>((sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<PrimOptions>>().Value;
    http.BaseAddress = new Uri(o.BaseUrl);
    http.DefaultRequestHeaders.Add("apikey", o.ApiKey);
    http.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IdfmOpenDataClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenDataOptions>>().Value.IdfmBaseUrl);
    http.Timeout = TimeSpan.FromMinutes(5);
})
// The portal sometimes answers exports gzipped; asking for it explicitly makes that decodable, and smaller.
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
builder.Services.AddHttpClient<VelibClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenDataOptions>>().Value.VelibBaseUrl);
    http.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<GeocodingClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenDataOptions>>().Value.GeocoderBaseUrl);
    http.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<PrimQuota>();
builder.Services.AddScoped<DeparturesService>();
builder.Services.AddScoped<DisruptionsService>();
builder.Services.AddScoped<VelibStatusService>();
builder.Services.AddScoped<NearbyService>();
builder.Services.AddScoped<StationSearchService>();
builder.Services.AddScoped<ReferentialImporter>();
builder.Services.AddHostedService<ReferentialRefreshService>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .WithMethods(HttpMethods.Get)));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<TabloDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(); // the web build is copied to wwwroot in the container image

app.MapGroup("/api")
    .MapTransitEndpoints()
    .MapGeocodingEndpoints();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();
