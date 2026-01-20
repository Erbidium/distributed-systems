using Cassandra;
using Orders.Materializer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICluster>(_ =>
{
    var host = Environment.GetEnvironmentVariable("CASSANDRA_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("CASSANDRA__Port") ?? "9042";
    return Cluster.Builder().AddContactPoint(host).WithPort(int.Parse(port)).Build();
});

builder.Services.AddSingleton(async sp =>
{
    var cluster = sp.GetRequiredService<ICluster>();
    var ks = Environment.GetEnvironmentVariable("CASSANDRA_KEYSPACE") ?? "lab";
    var session = await cluster.ConnectAsync();

    await session.ExecuteAsync(new SimpleStatement($@"
            CREATE KEYSPACE IF NOT EXISTS {ks}
            WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};"));

    var ksSession = await cluster.ConnectAsync(ks);

    await ksSession.ExecuteAsync(new SimpleStatement(@"
        CREATE TABLE IF NOT EXISTS orders_by_id (
          order_id uuid PRIMARY KEY,
          status text,
          total decimal,
          updated_at timestamp,
          last_seq bigint
        );"));

    var upsert = await ksSession.PrepareAsync(@"
        INSERT INTO orders_by_id (order_id, status, total, updated_at, last_seq)
        VALUES (?, ?, ?, ?, ?)");

    var select = await ksSession.PrepareAsync(@"
        SELECT last_seq, status, total FROM orders_by_id WHERE order_id=?");

    return (ksSession, upsert, select);
});

builder.Services.AddHostedService<MaterializerWorker>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
