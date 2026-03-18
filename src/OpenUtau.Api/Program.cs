using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

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

app.UseMiddleware<OpenUtau.Api.Middlewares.SessionMiddleware>();

app.MapControllers();
app.Run();
