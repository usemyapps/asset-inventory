using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using AssetInventory.Services;
using System.Security.Cryptography.X509Certificates;
using System.IO;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var serviceUrl = Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL") 
                        ?? builder.Configuration["DynamoDB:ServiceUrl"];
    var config = new AmazonDynamoDBConfig
    {
        ServiceURL = serviceUrl
    };
    return new AmazonDynamoDBClient(config);
});

builder.Services.AddSingleton<AssetService>();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(443, listenOptions =>
    {
        var certPath = Path.Combine("/app", "aspnetcore.pfx");
        if (File.Exists(certPath))
        {
            var cert = new X509Certificate2(certPath, (string)null, X509KeyStorageFlags.MachineKeySet);
            listenOptions.UseHttps(cert);
        }
        else
        {
            throw new FileNotFoundException("The certificate file was not found.", certPath);
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
