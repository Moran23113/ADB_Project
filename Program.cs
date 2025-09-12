var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IRestauracionRepositorio, RestauracionRepositorio>();
builder.Services.AddSingleton<IEsquemaRepositorio, EsquemaRepositorio>();
builder.Services.AddSingleton<ConstructorDiagramaChen>();
builder.Services.AddSingleton<ConstructorDiagramaRelacional>();
builder.Services.AddSingleton<ITraductorRelacional, TraductorRelacional>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=DiagramaEr}/{action=Subir}/{id?}");

app.Run();
