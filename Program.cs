using Harmonify.MusicServer.Auth;
using Harmonify.MusicServer.Models;
using Harmonify.MusicServer.Services;
using Harmonify.MusicServer.Web;
using Harmonify.MusicServer.Web.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
    }
  };
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAppEndpoints();

app.Run();
