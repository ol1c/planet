using OptiSync.Shared.Hosting;
using OptiSync.Worker.Threads;

return await WorkerProcessHost.RunAsync(args, new ThreadQueueWorkerEngine(Environment.ProcessorCount * 4 + 8));
