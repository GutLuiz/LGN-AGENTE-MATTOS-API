using LGN_AGENTE.Services.Agente;

var builder = WebApplication.CreateBuilder(args);

// Libera o frontend Next.js chamar o backend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();

// Registra o AgentService pra injeção de dependência
builder.Services.AddScoped<AgenteService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();