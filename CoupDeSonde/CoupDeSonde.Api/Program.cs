// Program.cs — placeholder pending full implementation in the next phase.
// The controllers, middleware, and service registrations will be added
// when the controllers and pipeline are implemented.

var builder = WebApplication.CreateBuilder(args);

// Add controllers (required for all new API controllers).
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
