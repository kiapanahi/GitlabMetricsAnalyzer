var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Toman_Management_KPIAnalysis_ApiService>("apiservice");

builder.AddProject<Projects.Toman_Management_KPIAnalysis_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
