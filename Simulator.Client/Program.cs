using BlazorDownloadFile;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OfficeOpenXml;
using Simulator.Client.Services;
using Simulator.Client.Services.ExportServices;
using Simulator.Client.Services.Navigations;
using UnitSystem;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder
.AddRootComponents()
.AddClientServices();
builder.Services.AddSingleton<NavigationBack>();

ExcelPackage.License.SetNonCommercialOrganization("AFDS");

// 3. Registrar el servicio de descargas de archivos
builder.Services.AddBlazorDownloadFile();

// 4. Registrar tu servicio de exportación (el que crearemos a continuación)
builder.Services.AddScoped<IPlantExportService, PlantExportService>();

builder.Services.AddBlazorDownloadFile();
UnitManager.RegisterByAssembly(typeof(SIUnitTypes).Assembly);
// Registro de servicios

var host = builder.Build();

host.Services.GetService<NavigationBack>();

await host.RunAsync();
