using static Toman.Management.KPIAnalysis.Constants;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Toman_Management_KPIAnalysis_ApiService>(Keys.GitlabMetricsCollector);

builder.Build().Run();
