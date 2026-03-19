using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using OpenUtau.Api.Security;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.Scheme, options =>
    {
        options.HeaderName = builder.Configuration["Auth:HeaderName"] ?? ApiKeyAuthenticationOptions.DefaultHeaderName;
        options.ApiKey = builder.Configuration["Auth:ApiKey"] ?? ApiKeyAuthenticationOptions.DefaultApiKey;
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(ApiKeyAuthenticationOptions.Scheme)
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// OpenUtau Core Initialization (Headless)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

DocManager.Inst.Initialize(Thread.CurrentThread, TaskScheduler.Default);
// Prevent NullReferenceException when core tries to update UI
DocManager.Inst.PostOnUIThread = action => {
    var field = typeof(DocManager).GetField("mainThread", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (field != null) {
        var oldThread = field.GetValue(DocManager.Inst);
        field.SetValue(DocManager.Inst, Thread.CurrentThread);
        action();
        field.SetValue(DocManager.Inst, oldThread);
    } else {
        action();
    }
};

SingerManager.Inst.Initialize();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OpenUtau.Api.Middlewares.SessionMiddleware>();
app.UseWebSockets();

app.MapControllers();
app.Run();

public partial class Program { }
