var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.Test>("test");
builder.Build().Run();
