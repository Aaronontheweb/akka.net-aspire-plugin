using Aaron.Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("akka-discovery");

var akka = builder.AddAkka("sample-cluster")
    .WithClustering(redis);

builder.AddProject<Projects.Aaron_Akka_Aspire_Sample_Service>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();
