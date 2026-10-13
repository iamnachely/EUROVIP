using Common;
using Common.Data.Entities;
using EuroVip.Helpers;
using EuroVip.Repositories.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Secretos locales (cadena de conexión con password) — archivo ignorado por git.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("Logs/eurovip-.log", rollingInterval: RollingInterval.Day));

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// ----- Identity sobre Dapper (patrón BackTiTan), tablas en esquema Eurovip -----
builder.Services.AddTransient<IUserStore<Usuario>, UsuarioStore>();
builder.Services.AddIdentityCore<Usuario>(opt =>
    {
        opt.Password.RequireDigit = true;
        opt.Password.RequireLowercase = true;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireNonAlphanumeric = true;
        opt.Password.RequiredLength = 8;
    })
    .AddErrorDescriber<MensajesError>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, opt =>
    {
        opt.Cookie.Name = "EuroVip.Identity";
        opt.Cookie.HttpOnly = true;
        opt.ExpireTimeSpan = TimeSpan.FromDays(1);
        opt.SlidingExpiration = true;
        opt.LoginPath = "/Auth/Login";
        opt.AccessDeniedPath = "/Auth/Denied";
    });

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<EuroVip.Repositories.IHotelRepository, EuroVip.Repositories.HotelRepository>();
builder.Services.AddScoped<EuroVip.Repositories.ICircuitoRepository, EuroVip.Repositories.CircuitoRepository>();
builder.Services.AddScoped<EuroVip.Repositories.ICargaCircuitoService, EuroVip.Repositories.CargaCircuitoService>();
builder.Services.AddScoped<EuroVip.Repositories.IExcelCircuitoParser, EuroVip.Repositories.ExcelCircuitoParser>();
builder.Services.AddScoped<EuroVip.Repositories.IReporteRepository, EuroVip.Repositories.ReporteRepository>();
builder.Services.AddScoped<EuroVip.Repositories.IOfertaRepository, EuroVip.Repositories.OfertaRepository>();
builder.Services.AddScoped<EuroVip.Repositories.ICargaOfertaService, EuroVip.Repositories.CargaOfertaService>();
builder.Services.AddScoped<EuroVip.Repositories.IExcelOfertaParser, EuroVip.Repositories.ExcelOfertaParser>();
builder.Services.AddScoped<EuroVip.Repositories.IHerramientasRepository, EuroVip.Repositories.HerramientasRepository>();
builder.Services.AddScoped<EuroVip.Repositories.IHomeWebRepository, EuroVip.Repositories.HomeWebRepository>();
builder.Services.AddScoped<EuroVip.Repositories.IPerfilRepository, EuroVip.Repositories.PerfilRepository>();
builder.Services.AddScoped<EuroVip.Repositories.IConsultaRepository, EuroVip.Repositories.ConsultaRepository>();
builder.Services.AddScoped<EuroVip.Repositories.IFlyerRepository, EuroVip.Repositories.FlyerRepository>();
builder.Services.AddScoped<EuroVip.Services.IFlyerImagenService, EuroVip.Services.FlyerImagenService>();
builder.Services.AddScoped<EuroVip.Services.WhatsAppService>();
builder.Services.AddSingleton<EuroVip.Services.FlyerEnvioJob>();
builder.Services.AddHttpClient("whatsapp");

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// Todo requiere login salvo lo marcado con [AllowAnonymous]
var mvc = builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())));
if (builder.Environment.IsDevelopment())
    mvc.AddRazorRuntimeCompilation();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// La sesión vive en memoria: si el servidor se reinicia, la cookie de login sigue válida pero se pierden
// nombre, rol y foto. Se recargan desde Eurovip.usuario la primera vez que hacen falta.
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true && ctx.Session.GetString("NombreUsuario") is null)
    {
        var p = await ctx.RequestServices.GetRequiredService<EuroVip.Repositories.IPerfilRepository>().PerfilAsync(ctx.User.Identity.Name ?? "");
        if (p is not null)
        {
            ctx.Session.SetString("UserName", p.UserName);
            ctx.Session.SetString("NombreUsuario", $"{p.Nombre} {p.Apellido}".Trim());
            ctx.Session.SetString("Rol", p.Rol ?? "");
            if (p.Foto is not null) ctx.Session.SetString("Imagen", p.Foto);
        }
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
