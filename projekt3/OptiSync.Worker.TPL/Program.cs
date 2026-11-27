using OptiSync.Shared.Hosting;
using OptiSync.Worker.TPL;

return await WorkerProcessHost.RunAsync(args, new TplWorkerEngine(Environment.ProcessorCount * 4 + 8));
