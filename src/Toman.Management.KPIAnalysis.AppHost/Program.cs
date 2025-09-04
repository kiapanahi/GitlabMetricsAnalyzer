using static Toman.Management.KPIAnalysis.Constants;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> pgUsername = builder.AddParameter("username", "root", secret: true);
IResourceBuilder<ParameterResource> pgPassword = builder.AddParameter("password", "root", secret: true);
IResourceBuilder<PostgresServerResource> postgresServer = builder.AddPostgres(Keys.PostgresService, pgUsername, pgPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<PostgresDatabaseResource> postgresdb = postgresServer.AddDatabase(Keys.PostgresDatabase);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Toman_Management_KPIAnalysis_ApiService>(Keys.GitlabMetricsCollector)
    .WithReference(postgresdb)
    .WaitFor(postgresdb);


builder.Build().Run();
