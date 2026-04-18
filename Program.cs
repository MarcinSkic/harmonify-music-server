using System.Reflection;
using Harmonify.MusicServer.Auth;
using Harmonify.MusicServer.Models;
using Harmonify.MusicServer.Services;
using Harmonify.MusicServer.Web;
using Harmonify.MusicServer.Web.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Insert(0, new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
{
  InitialData = new Dictionary<string, string?>
  {
    [$"{MusicServerOptions.SectionName}:MusicDirectory"] = "./music",
    [$"{MusicServerOptions.SectionName}:Username"] = "harmonify",
    [$"{MusicServerOptions.SectionName}:Password"] = "mPGhyM8Pqr77hoH4",
    ["Urls"] = "http://localhost:51234",
  }
});

var serverConfigSection = builder.Configuration.GetSection(MusicServerOptions.SectionName);
// Configuration
builder.Services.Configure<MusicServerOptions>(
  serverConfigSection
);

// Authentication & Authorization
builder.Services
  .AddAuthentication(BasicAuthHandler.SchemeName)
  .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(BasicAuthHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<IFileSystemScanner, FileSystemScanner>();
builder.Services.AddHttpClient();

// CORS
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    policy
      .AllowAnyOrigin()
      .AllowAnyHeader()
      .AllowAnyMethod()
      .WithExposedHeaders("Content-Range", "Accept-Ranges", "Content-Length");
  });
});

// OpenAPI
builder.Services.AddOpenApi(options =>
{
  options.AddDocumentTransformer<BasicAuthenticationSchemeTransformer>();
});

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

var musicServerConfig = serverConfigSection
  .Get<MusicServerOptions>()!;

var embeddedProvider = new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "wwwroot");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedProvider });

app.MapOpenApi();
app.MapScalarApiReference("/api", options =>
{
  options.Title = "Harmonify Music Server";
  options.Authentication = new ScalarAuthenticationOptions()
  {
    SecuritySchemes = new Dictionary<string, ScalarSecurityScheme>
    {
      ["Basic"] = new ScalarHttpSecurityScheme
      {
        Username = musicServerConfig.Username,
        Password = musicServerConfig.Password,
      }
    },
    PreferredSecuritySchemes = ["Basic"]
  };
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAppEndpoints();
app.MapFallbackToFile("index.html");

app.Services.GetRequiredService<IFileSystemScanner>().Scan();

app.Run();
