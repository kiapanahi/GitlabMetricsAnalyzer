using static KuriousLabs.Management.KPIAnalysis.Constants;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.KuriousLabs_Management_KPIAnalysis_ApiService>(Keys.GitlabMetricsCollector);

builder.Build().Run();
