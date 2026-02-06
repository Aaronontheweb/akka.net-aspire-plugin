using Aaron.Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("azure-storage").RunAsEmulator();
var tables = storage.AddTables("akka-discovery");

var akka = builder.AddAkka("sample-cluster")
    .WithClustering(tables);

builder.AddProject<Projects.Aaron_Akka_Aspire_Sample_Azure_Service>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();
