using ApplicationSebrae.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Adicionar suporte a sessões
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Sessão de 60 minutos
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".MissaoSebrae.Session";
});

// Adicionar suporte a memória distribuída (para sessões)
builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<VotingService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<SessionManagementService>(); // ← Adicione esta linha
builder.Services.AddScoped<RoomService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANTE: UseSession deve vir antes de UseAuthorization
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();