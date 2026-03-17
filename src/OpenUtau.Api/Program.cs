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
DocManager.Inst.PostOnUIThread = action => Task.Run(action);

SingerManager.Inst.Initialize();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
